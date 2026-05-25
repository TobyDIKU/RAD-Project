using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Emit;
using System.Runtime.InteropServices.Swift;
using System.Globalization;
using System.Diagnostics;
using System.Xml.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

//generation of random the random bytes in the 100 experiments
using System.Security.Cryptography;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

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

    public UInt128 SquareSumOfContents()
    {
        Node curr;
        UInt128 sum = UInt128.Zero;
        foreach (Node head in table)
        {
            curr = head;
            while (curr != null)
            {
                sum += (UInt128)(curr.Val * curr.Val);
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

public class BCS
{
    private UInt128[] A;
    private long[] C_table;

    private Func<UInt128[], ulong, UInt128> g;

    int t;
    private (ulong, int) Compute(int t, Func<UInt128[], ulong, UInt128> g, ulong x)
    {
        if (t > 64 || t < 0) throw new ArgumentOutOfRangeException(nameof(t));
        UInt128 f = g(A, x);

        // f mod 2^t
        ulong h = (ulong)(f & (t == 64 ? ulong.MaxValue : (1UL << t) - 1));


        int s = (int)(1 - 2 * (f >> 88));
        return (h, s);
    }

    private void Process(ulong x, int val)
    {
        (ulong h, int s) = Compute(t, g, x);
        C_table[h] = C_table[h] + s * val;
        return;
    }

    public void Process_stream(IEnumerable<Tuple<ulong, int>> stream)
    {
        foreach (var (key, value) in stream)
        {
            Process(key, value);
        }
        return;
    }

    public UInt128 BCS_2nd_Moment()
    {
        UInt128 sum = UInt128.Zero;
        foreach (long num in C_table)
        {
            sum += (UInt128)(num * num);
        }
        return sum;
    }

    public BCS(int t, UInt128[] A, Func<UInt128[], ulong, UInt128> g)
    {
        if (t > 64 || t < 0) throw new ArgumentOutOfRangeException(nameof(t));
        if (A.Length != 4) throw new ArgumentException("A must be length 4", nameof(A));

        this.g = g;
        this.t = t;
        this.A = A;
        this.C_table = new long[1UL << t];
    }
}

class Program
{
    static ulong h1_a = 0x89661511BDA67731UL; // www.random.org/bytes
    static UInt128 p = (UInt128.One << 89) - 1; //mersene prime 2^89 - 1


    //func for uintint mod p
    static UInt128 ModP(UInt128 y) { y = (y & p) + (y >> 89); return y >= p ? y - p : y; }


    //func for converting byte arrays
    static UInt128 BytesToUInt128(byte[] bytes)
    {
        UInt128 result = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            result |= (UInt128)bytes[i] << (8 * i);
        }

        return result;
    }

    //a and b retrival with www.random.org/bytes
    static byte[] h2_abytes = new byte[] { 0x0d, 0x08, 0xa0, 0x18, 0x82, 0xef, 0x8e, 0x56, 0x30, 0x89, 0xea, 0xc0 };
    static byte[] h2_bbytes = new byte[] { 0x91, 0xbb, 0xde, 0x68, 0x2c, 0x6f, 0x6c, 0x60, 0x9d, 0x3d, 0xf3, 0xbf };
    static UInt128 h2_a = ModP(BytesToUInt128(h2_abytes));
    static UInt128 h2_b = ModP(BytesToUInt128(h2_bbytes));

    static byte[] h4_a0bytes = new byte[] { 0x65, 0x97, 0xb5, 0x73, 0xb2, 0x91, 0x83, 0x0f, 0x12, 0xc2, 0xa4, 0xdf };
    static byte[] h4_a1bytes = new byte[] { 0xbc, 0x89, 0x3d, 0xa3, 0x8a, 0x8c, 0xd4, 0x26, 0x98, 0xd0, 0x12, 0xba };
    static byte[] h4_a2bytes = new byte[] { 0x29, 0xe2, 0xf8, 0xe6, 0xec, 0x4d, 0xde, 0x13, 0xe2, 0x6f, 0x3b, 0x0b };
    static byte[] h4_a3bytes = new byte[] { 0x0d, 0xdc, 0x76, 0x92, 0x2f, 0x67, 0xa7, 0x88, 0x8e, 0xb0, 0x0f, 0xa9 };

    static UInt128 h4_a0 = ModP(BytesToUInt128(h4_a0bytes));
    static UInt128 h4_a1 = ModP(BytesToUInt128(h4_a1bytes));
    static UInt128 h4_a2 = ModP(BytesToUInt128(h4_a2bytes));
    static UInt128 h4_a3 = ModP(BytesToUInt128(h4_a3bytes));

    static ulong MASK25 = (1UL << 25) - 1;

    static UInt128[] h4_A = [h4_a0, h4_a1, h4_a2, h4_a3];



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

    static ulong multiplyModPrime(UInt128 a, UInt128 b, int l, ulong x)
    {
        const ulong MASK25 = (1UL << 25) - 1;

        // split a
        ulong a_lo = (ulong)a;
        ulong a_hi = (ulong)(a >> 64);

        // split b
        ulong b_lo = (ulong)b;
        ulong b_hi = (ulong)(b >> 64);

        // a*x
        UInt128 p0 = (UInt128)a_lo * x;
        UInt128 p1 = (UInt128)a_hi * x;

        // 153-bit accumulator
        ulong z0 = (ulong)p0;

        ulong z1 =
            (ulong)(p0 >> 64) +
            (ulong)p1;

        ulong z2 =
            (ulong)(p1 >> 64);

        // + b
        ulong old = z0;
        z0 += b_lo;

        ulong carry = (z0 < old) ? 1UL : 0UL;

        z1 += b_hi + carry;

        if (z1 < b_hi + carry)
            z2++;

        // reduction mod 2^89-1
        UInt128 low =
            (UInt128)z0 |
            ((UInt128)(z1 & MASK25) << 64);

        UInt128 high =
            (UInt128)(z1 >> 25) |
            ((UInt128)z2 << 39);

        UInt128 y = low + high;

        y = (y & p) + (y >> 89);

        if (y >= p)
            y -= p;

        return (ulong)y & ((1UL << l) - 1);
    }

    static UInt128 Four_Universal_Hashing(UInt128[] A, ulong x)
    {
        if (A.Length != 4) { throw new ArgumentException("A must be length 4", nameof(A)); }
        ulong y_lo = (ulong)A[3];
        ulong y_hi = (ulong)(A[3] >> 64);
        for (int i = 2; i >= 0; i--)
        {
            ulong a_lo = (ulong)A[i];
            ulong a_hi = (ulong)(A[i] >> 64);

            // y*x
            UInt128 p0 = (UInt128)y_lo * x;
            UInt128 p1 = (UInt128)y_hi * x;

            ulong z0 = (ulong)p0;

            ulong z1 =
                (ulong)(p0 >> 64) +
                (ulong)p1;

            ulong z2 =
                (ulong)(p1 >> 64);

            // + coefficient
            ulong old = z0;
            z0 += a_lo;

            ulong carry = (z0 < old) ? 1UL : 0UL;

            z1 += a_hi + carry;

            if (z1 < a_hi + carry)
                z2++;

            // reduction mod 2^89-1
            UInt128 low =
                (UInt128)z0 |
                ((UInt128)(z1 & MASK25) << 64);

            UInt128 high =
                (UInt128)(z1 >> 25) |
                ((UInt128)z2 << 39);

            UInt128 y = low + high;

            y = (y & p) + (y >> 89);

            if (y >= p)
                y -= p;

            y_lo = (ulong)y;
            y_hi = (ulong)(y >> 64);

        }
        return
        (UInt128)y_lo |
        ((UInt128)y_hi << 64);
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


    static UInt128 SquareSumStream(Func<ulong, int, ulong> h, int l, IEnumerable<Tuple<ulong, int>> stream)
    {
        Chained_hashtable table = new Chained_hashtable(h, l);
        foreach (var (key, value) in stream)
        {
            table.increment(key, value);
        }
        return table.SquareSumOfContents();
    }


    static UInt128 RandomModP()
    {
        UInt128 val;
        do
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(12);
            val = ModP(BytesToUInt128(bytes));
        } while (val == 0);

        return val;
    }


    static void Main()
    {
        int[] size_array = [1000, 10000, 100000, 1000000];
        int[] l_array = [12, 14, 14, 16];

        IEnumerable<Tuple<ulong, int>> stream;

        DateTime start, end;
        TimeSpan MS_time, MMP_time;
        UInt128 MS_sum;
        UInt128 MMP_sum;

        for (int i = 0; i < size_array.Length; i++)
        {
            MS_sum = UInt128.Zero;
            MMP_sum = UInt128.Zero;

            stream = CreateStream(size_array[i], l_array[i]);

            start = DateTime.Now;
            foreach (var (key, value) in stream)
            {
                MS_sum += multiplyShift(h1_a, l_array[i], key);
            }
            end = DateTime.Now;
            MS_time = end - start;

            start = DateTime.Now;
            foreach (var (key, value) in stream)
            {
                MMP_sum += multiplyModPrime(h2_a, h2_b, l_array[i], key);
            }
            end = DateTime.Now;
            MMP_time = end - start;

            Console.WriteLine($"Size of stream: {size_array[i]}   l size: {l_array[i]}\n");
            Console.WriteLine($"Multiply shift sum: {MS_sum}");
            Console.WriteLine($"Multiply mod prime sum: {MMP_sum}\n");
            Console.WriteLine($"Multiply shift time (ms): {MS_time.TotalMilliseconds}");
            Console.WriteLine($"Multiply mod prime time (ms): {MMP_time.TotalMilliseconds}\n\n");
        }

        // Opgave 3 arrays
        int[] opgave3_size_array = [2000000, 2000000, 2000000, 2000000, 2000000];
        int[] opgave3_l_array = [12, 14, 16, 18, 20];

        // Loopoopsætningen er rettet til at bruge opgave3_size_array.Length
        for (int i = 0; i < opgave3_size_array.Length; i++)
        {
            stream = CreateStream(opgave3_size_array[i], opgave3_l_array[i]);

            start = DateTime.Now;
            // Rettet l_array[i] til opgave3_l_array[i]
            MS_sum = SquareSumStream((x, lValue) => multiplyShift(h1_a, lValue, x), opgave3_l_array[i], stream);
            end = DateTime.Now;
            MS_time = end - start;

            start = DateTime.Now;
            // Rettet l_array[i] til opgave3_l_array[i]
            MMP_sum = SquareSumStream((x, lValue) => multiplyModPrime(h2_a, h2_b, lValue, x), opgave3_l_array[i], stream);
            end = DateTime.Now;
            MMP_time = end - start;

            Console.WriteLine($"Size of stream: {opgave3_size_array[i]}   l size: {opgave3_l_array[i]}");
            Console.WriteLine($"Multiply shift squared sum: {MS_sum}");
            Console.WriteLine($"Multiply mod prime squared sum: {MMP_sum}");
            Console.WriteLine($"Multiply shift time (ms): {MS_time.TotalMilliseconds}");
            Console.WriteLine($"Multiply mod prime time (ms): {MMP_time.TotalMilliseconds}\n");
        }


        //100 experiments
        int l = 16;
        stream = CreateStream(1000000, l);
        UInt128 a1, a2, a3, a4;
        UInt128 skecth_2nd_moment, exact_2nd_moment;
        exact_2nd_moment = SquareSumStream((x, l) => multiplyModPrime(h2_a, h2_b, l, x), l, stream);
        UInt128[] a_array;
        BCS skecth;
        UInt128[] experiments = new UInt128[100];

        for (int i = 0; i < 100; i++)
        {
            a1 = RandomModP();
            a2 = RandomModP();
            a3 = RandomModP();
            a4 = RandomModP();
            a_array = new UInt128[] { a1, a2, a3, a4 };
            skecth = new BCS(l, a_array, Four_Universal_Hashing);
            skecth.Process_stream(stream);
            skecth_2nd_moment = skecth.BCS_2nd_Moment();
            experiments[i] = skecth_2nd_moment;
        }


        // Sort experiments
        var sorted = experiments.Select(x => x).OrderBy(x => x).ToArray();

        // write to CSV file for analysis
        using (var writer = new StreamWriter("sorted_ results.csv"))
        {
            writer.WriteLine("rank,estimate,exact");
            for (int i = 0; i < 100; i++)
            {
                writer.WriteLine($"{i + 1},{sorted[i]},{exact_2nd_moment}");
            }
        }

        // Del i 9 grupper af størrelse 11
        UInt128[] medians = new UInt128[9];
        for (int i = 0; i < 9; i++)
        {
            UInt128[] group = experiments.Skip(i * 11).Take(11).ToArray();
            Array.Sort(group);
            medians[i] = group[5];
        }

        Array.Sort(medians);
        using (var writer = new StreamWriter("medians.csv"))
        {
            writer.WriteLine("rank,median,exact");
            for (int i = 0; i < 9; i++)
            {
                writer.WriteLine($"{i + 1},{medians[i]},{exact_2nd_moment}");
            }
        }


        (UInt128, TimeSpan)[][] experiments_matrix = new (UInt128, TimeSpan)[3][];
        UInt128[][] medians_matrix = new UInt128[3][];
        for (int i = 0; i < 3; i++)
        {
            experiments_matrix[i] = new (UInt128, TimeSpan)[100];
            medians_matrix[i] = new UInt128[9];
        }

        //differing l sizes
        l_array = [12, 14, 18];


        //doing experiments for each m = (2^l) size
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                a1 = RandomModP();
                a2 = RandomModP();
                a3 = RandomModP();
                a4 = RandomModP();
                start = DateTime.Now;
                a_array = new UInt128[] { a1, a2, a3, a4 };
                skecth = new BCS(l_array[i], a_array, Four_Universal_Hashing);
                skecth.Process_stream(stream);
                skecth_2nd_moment = skecth.BCS_2nd_Moment();
                end = DateTime.Now;
                experiments_matrix[i][j] = (skecth_2nd_moment, end - start);
            }
        }

        //finding sorted version for each m size 
        (UInt128, TimeSpan)[][] sorted_matrix = new (UInt128, TimeSpan)[3][];
        for (int i = 0; i < 3; i++)
        {
            sorted_matrix[i] = experiments_matrix[i].Select(x => x).OrderBy(x => x.Item1).ToArray();
        }

        //writing to csv sorted matrix csv file
        using (var writer = new StreamWriter("sorted_matrix.csv"))
        {
            writer.WriteLine("rank,estimate,exact,l,time");
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    writer.WriteLine($"{j + 1},{sorted_matrix[i][j].Item1},{exact_2nd_moment},{l_array[i]},{sorted_matrix[i][j].Item2.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
            }
        }


        //finding median values for the experiments for each m size
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                UInt128[] group = experiments_matrix[i].Skip(j * 11).Take(11).Select(x => x.Item1).ToArray();
                Array.Sort(group);
                medians_matrix[i][j] = group[5];
            }

            Array.Sort(medians_matrix[i]);
        }


        //writing to csv median matrix csv file
        using (var writer = new StreamWriter("medians_matrix.csv"))
        {
            writer.WriteLine("rank,median,exact,l");
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    writer.WriteLine($"{j + 1},{medians_matrix[i][j]},{exact_2nd_moment},{l_array[i]}");
                }
            }
        }

    }
}
