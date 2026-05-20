using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.InteropServices.Swift;
using System.Globalization;

//node class for chaining
public class Node
{
    public int Val { get; set; }
    public ulong Key { get; set; }
    public Node Next { get; set; } = null;

    public Node(ulong key, int val)
    {
        Val = val;
        Key = key;
    }
}

//hashtable with chaining
public class Chained_hashtable
{
    public readonly int l;
    private Node[] table;

    private Func<ulong, int, ulong> h;

    public int get(ulong x)
    {
        Node curr = this.table[this.h(x, this.l)];
        while (curr is not null)
        {
            if (curr.Key == x) return curr.Val;
            curr = curr.Next;
        }
        return 0;
    }

    public void set(ulong x, int v)
    {
        ulong index = this.h(x, this.l);
        Node head = this.table[index];
        Node curr = head;
        while (curr is not null)
        {
            if (curr.Key == x)
            {
                curr.Val = v;
                return;
            }
            curr = curr.Next;
        }

        Node newNode = new Node(x, v);
        newNode.Next = head;
        this.table[index] = newNode;
        return;
    }

    public void increment(ulong x, int d)
    {
        ulong index = this.h(x, this.l);
        Node head = this.table[index];
        Node curr = head;
        while (curr is not null)
        {
            if (curr.Key == x)
            {
                curr.Val += d;
                return;
            }
            curr = curr.Next;
        }

        Node newNode = new Node(x, d);
        newNode.Next = head;
        this.table[index] = newNode;
        return;
    }

    public BigInteger SquareSumOfContents()
    {
        Node curr;
        BigInteger sum = BigInteger.Zero;
        foreach (Node head in table)
        {
            curr = head;
            while (curr != null)
            {
                sum += (BigInteger)curr.Val * curr.Val;
                curr = curr.Next;
            }
        }
        return sum;
    }

    public Chained_hashtable(Func<ulong, int, ulong> h, int l)
    {
        this.l = l;
        this.h = h;
        //make table of size min of 2^l or 2^20
        int power = Math.Min(20, l);
        this.table = new Node[1 << l];
    }

}

class Program
{
    static ulong h1_a = 0x89661511BDA67731UL; // www.random.org/bytes
    static BigInteger p = (BigInteger.One << 89) - 1; //mersene prime 2^89 - 1

    //a and b retrival with www.random.org/bytes
    static byte[] h2_abytes = new byte[] { 0x0d, 0x08, 0xa0, 0x18, 0x82, 0xef, 0x8e, 0x56, 0x30, 0x89, 0xea, 0xc0 };
    static byte[] h2_bbytes = new byte[] { 0x91, 0xbb, 0xde, 0x68, 0x2c, 0x6f, 0x6c, 0x60, 0x9d, 0x3d, 0xf3, 0xbf };
    static BigInteger mask = (BigInteger.One << 89) - 1;
    static BigInteger h2_a = new BigInteger(h2_abytes, isUnsigned: true) & mask;
    static BigInteger h2_b = new BigInteger(h2_bbytes, isUnsigned: true) & mask;

    static ulong multiplyShift(ulong a, int l, ulong x)
    {
        if (l <= 0 || l >= 64)
        {
            throw new ArgumentOutOfRangeException(nameof(l));
        }

        if ((a & 1) != 1)
        {
            throw new ArgumentException("a must be odd", nameof(a));
        }
        return (a * x) >> (64 - l);
    }

    static ulong multiplyModPrime(BigInteger a, BigInteger b, int l, ulong x)
    {
        BigInteger y = a * x + b;

        //find a*X + b mod p
        y = (y & p) + (y >> 89);
        if (y >= p) y -= p;

        // mod 2^l  (it is a power of two)
        return (ulong)(y & ((1UL << l) - 1));
    }



    static IEnumerable<Tuple<ulong, int>> CreateStream(int n, int l)
    {
        // We generate a random uint64 number .
        Random rnd = new System.Random();
        ulong a = 0UL;
        Byte[] b = new Byte[8];
        rnd.NextBytes(b);
        for (int i = 0; i < 8; ++i)
        {
            a = (a << 8) + (ulong)b[i];
        }
        // We demand that our random number has 30 zeros on the least significant bits and then a one .
        a = (a | ((1UL << 31) - 1UL)) ^ ((1UL << 30) - 1UL);
        ulong x = 0UL;
        for (int i = 0; i < n / 3; ++i)
        {
            x = x + a;
            yield return Tuple.Create(x & (((1UL << l) - 1UL) <<
            30), 1);
        }
        for (int i = 0; i < (n + 1) / 3; ++i)
        {
            x = x + a;
            yield return Tuple.Create(x & (((1UL << l) - 1UL) << 30), -1);
        }
        for (int i = 0; i < (n + 2) / 3; ++i)
        {
            x = x + a;
            yield return Tuple.Create(x & (((1UL << l) - 1UL) << 30), 1);
        }
    }


    static BigInteger SquareSumStream(Func<ulong, int, ulong> h, int l, IEnumerable<Tuple<ulong, int>> stream)
    {
        Chained_hashtable table = new Chained_hashtable(h, l);
        foreach (var (key, value) in stream)
        {
            table.increment(key, value);
        }
        return table.SquareSumOfContents();
    }


    static void Main()
    {
        int[] size_array = [1000, 10000, 100000, 1000000];

        int[] l_array = [12, 14, 14, 16];

        IEnumerable<Tuple<ulong, int>> stream;

        DateTime start, end;
        TimeSpan MS_time, MMP_time;
        BigInteger MS_sum;
        BigInteger MMP_sum;

        for (int i = 0; i < size_array.Length; i++)
        {
            // MS_time = TimeSpan.Zero;
            // MMP_time = TimeSpan.Zero;

            MS_sum = BigInteger.Zero;
            MMP_sum = BigInteger.Zero;

            stream = CreateStream(size_array[i], l_array[i]);

            //time to hash with multiplyshift
            start = DateTime.Now;
            foreach (var (key, value) in stream)
            {
                MS_sum += multiplyShift(h1_a, l_array[i], key);
            }
            end = DateTime.Now;
            MS_time = end - start;

            //time to hash with multiply-Mod-Prime
            start = DateTime.Now;
            foreach (var (key, value) in stream)
            {
                MMP_sum += multiplyModPrime(h2_a, h2_b, l_array[i], key);
            }
            end = DateTime.Now;
            MMP_time = end - start;

            //output sums and time
            Console.WriteLine($"Size of stream: {size_array[i]}   l size: {l_array[i]}\n");
            Console.WriteLine($"Muiltiply shift sum: {MS_sum}");
            Console.WriteLine($"Muiltiply mod prime sum: {MMP_sum}\n");
            Console.WriteLine($"Muiltiply shift time (ms): {MS_time.TotalMilliseconds}");
            Console.WriteLine($"Muiltiply mod prime time (ms): {MMP_time.TotalMilliseconds}\n\n");
        }


        //TODO: change these arrays for analysis
        int[] size_array = [1000, 10000, 100000, 1000000];

        int[] l_array = [12, 14, 14, 16];

        for (int i = 0; i < size_array.Length; i++)
        {
            // MS_time = TimeSpan.Zero;
            // MMP_time = TimeSpan.Zero;

            stream = CreateStream(size_array[i], l_array[i]);

            //Squared sum with chained hash table using multiply shift
            start = DateTime.Now;
            MS_sum = SquareSumStream((x, l) => multiplyShift(h1_a, l, x), l_array[i], stream);
            end = DateTime.Now;
            MS_time = end - start;

            //Squared sum with chained hash table using multiply mod prime
            start = DateTime.Now;
            MMP_sum = SquareSumStream((x, l) => multiplyModPrime(h2_a, h2_b, l, x), l_array[i], stream);
            end = DateTime.Now;
            MMP_time = end - start;

            //output squared sums and times.
            Console.WriteLine($"Size of stream: {size_array[i]}   l size: {l_array[i]}\n");
            Console.WriteLine($"Muiltiply shift squared sum: {MS_sum}");
            Console.WriteLine($"Muiltiply mod prime squared sum: {MMP_sum}\n");
            Console.WriteLine($"Muiltiply shift time squared sum (ms): {MS_time.TotalMilliseconds}");
            Console.WriteLine($"Muiltiply mod prime squared sum time (ms): {MMP_time.TotalMilliseconds}\n\n");
        }

    }

}

