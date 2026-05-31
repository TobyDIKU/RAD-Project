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
    public long Val { get; set; }           //the value of the node
    public ulong Key { get; set; }          //the key of the node
    public Node Next { get; set; } = null;  //pointer to the next node in the chain

    public Node(ulong key, int val)         //constructor for the node
    {
        Val = val;                          
        Key = key;                         
    }
}

//hashtable with chaining
public class Chained_hashtable
{
    public readonly int l;                          //the number of bits for the hash function
    private Node[] table;                           //the table of nodes    

    private Func<ulong, int, ulong> h;              //the hash function


    //get the value of a key in the hashtable, return 0 if not found
    public long get(ulong x)                        
    {
        Node curr = this.table[this.h(x, this.l)];  //get the head of the chain for the key x
        while (curr is not null)
        {
            if (curr.Key == x) return curr.Val;     
            curr = curr.Next;
        }
        return 0;
    }

    //set the value of a key in the hashtable, if the key is not found, add a new node to the chain
    public void set(ulong x, int v)                 
    {
        ulong index = this.h(x, this.l);            //get the index for the key x
        Node head = this.table[index];              //get the head of the chain for the key x
        Node curr = head;                           //set the current node to the head of the chain
        while (curr is not null)
        {
            if (curr.Key == x)
            {
                curr.Val = v;
                return;
            }
            curr = curr.Next;
        }

        Node newNode = new Node(x, v);              //create a new node with the key x and value v
        newNode.Next = head;                        //set the next pointer of the new node to the head of the chain
        this.table[index] = newNode;                //set the head of the chain to the new node
        return;
    }

    //increment the value of a key in the hashtable by d, if the key is not found, add a new node to the chain with value d
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


    //calculate the square sum of the values in the hashtable, by iterating through all the chains and summing the squares of the values
    public long SquareSumOfContents()
    {
        Node curr;                              
        long sum = 0L;                          
        foreach (Node head in table)
        {
            curr = head;
            while (curr != null)
            {
                sum += curr.Val * curr.Val;
                curr = curr.Next;
            }
        }
        return sum;
    }

    //constructor for the hashtable
    public Chained_hashtable(Func<ulong, int, ulong> h, int l)
    {
        this.l = l;
        this.h = h;
        //make table of size min of 2^l or 2^20
        int power = Math.Min(20, l);                
        this.table = new Node[1 << l];              
    }

}

//BCS sketch class
public class BCS
{
    private BigInteger[] A;                             //the array of random variables for the hash function
    private long[] C_table;                             //the table of counts for the hash function

    private Func<BigInteger[], ulong, BigInteger> g;    //the hash function

    int t;

    //compute the hash value and the sign for a given key x
    private (ulong, int) Compute(int t, Func<BigInteger[], ulong, BigInteger> g, ulong x)   
    {
        if (t > 64 || t < 0) throw new ArgumentOutOfRangeException(nameof(t));  //check that t is between 0 and 64
        BigInteger f = g(A, x);     //compute the hash value using the hash function g and the array of random variables A

        
        ulong h = (ulong)(f & (t == 64 ? ulong.MaxValue : (1UL << t) - 1));  //f mod 2^t

        int s = (int)(1 - 2 * (f >> 88));                                    //determine the sign
        return (h, s);                                                       //return the hash value and the sign
    }

    //process key x with value val, by computing the hash value and the sign for the key x and updating the count in the table Ctable
    private void Process(ulong x, int val)
    {
        (ulong h, int s) = Compute(t, g, x);
        C_table[h] = C_table[h] + s * val;
        return;
    }

    //process a stream of key-value pairs by calling the Process function for each key-value pair in the stream 
    public void Process_stream(IEnumerable<Tuple<ulong, int>> stream)
    {
        foreach (var (key, value) in stream)
        {
            Process(key, value);
        }
        return;
    }

    //calculate the 2nd moment of the count table by summing the squares of the counts in the table C_table
    public ulong BCS_2nd_Moment()
    {
        long sum = 0L;
        foreach (long num in C_table)
        {
            sum += num * num;
        }
        return (ulong)sum;
    }

    //constructor for the BCS sketch class
    public BCS(int t, BigInteger[] A, Func<BigInteger[], ulong, BigInteger> g)
    {
        if (t > 64 || t < 0) throw new ArgumentOutOfRangeException(nameof(t));          //check that t is between 0 and 64
        if (A.Length != 4) throw new ArgumentException("A must be length 4", nameof(A)); //check that A is of length 4

        this.g = g;
        this.t = t;
        this.A = A;
        this.C_table = new long[1UL << t];  //initialize the count table to be of size 2^t
    }
}

class Program
{
    static ulong h1_a = 0x89661511BDA67731UL; // www.random.org/bytes
    static BigInteger p = (BigInteger.One << 89) - 1; //mersene prime 2^89 - 1


    //func for mod p
    static BigInteger ModP(BigInteger y) { y = (y & p) + (y >> 89); return y >= p ? y - p : y; }


    //func for converting byte arrays
    static BigInteger BytesToUInt128(byte[] bytes)
    {
        return new BigInteger(bytes, isUnsigned: true);
    }

    //a and b retrival with www.random.org/bytes
    static byte[] h2_abytes = new byte[] { 0x0d, 0x08, 0xa0, 0x18, 0x82, 0xef, 0x8e, 0x56, 0x30, 0x89, 0xea, 0xc0 };
    static byte[] h2_bbytes = new byte[] { 0x91, 0xbb, 0xde, 0x68, 0x2c, 0x6f, 0x6c, 0x60, 0x9d, 0x3d, 0xf3, 0xbf };
    static BigInteger h2_a = ModP(BytesToUInt128(h2_abytes));
    static BigInteger h2_b = ModP(BytesToUInt128(h2_bbytes));

    static byte[] h4_a0bytes = new byte[] { 0x65, 0x97, 0xb5, 0x73, 0xb2, 0x91, 0x83, 0x0f, 0x12, 0xc2, 0xa4, 0xdf };
    static byte[] h4_a1bytes = new byte[] { 0xbc, 0x89, 0x3d, 0xa3, 0x8a, 0x8c, 0xd4, 0x26, 0x98, 0xd0, 0x12, 0xba };
    static byte[] h4_a2bytes = new byte[] { 0x29, 0xe2, 0xf8, 0xe6, 0xec, 0x4d, 0xde, 0x13, 0xe2, 0x6f, 0x3b, 0x0b };
    static byte[] h4_a3bytes = new byte[] { 0x0d, 0xdc, 0x76, 0x92, 0x2f, 0x67, 0xa7, 0x88, 0x8e, 0xb0, 0x0f, 0xa9 };

    static BigInteger h4_a0 = ModP(BytesToUInt128(h4_a0bytes));
    static BigInteger h4_a1 = ModP(BytesToUInt128(h4_a1bytes));
    static BigInteger h4_a2 = ModP(BytesToUInt128(h4_a2bytes));
    static BigInteger h4_a3 = ModP(BytesToUInt128(h4_a3bytes));

    static BigInteger[] h4_A = [h4_a0, h4_a1, h4_a2, h4_a3];


    //func for multiply shift, which multiplies a by x and then shifts the result to the right by (64 - l) bits
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

    //func for multiply mod prime, which multiplies a by x and adds b, then takes the result mod p and returns the least significant l bits of the result
    static ulong multiplyModPrime(BigInteger a, BigInteger b, int l, ulong x)
    {
        if (l < 0 || l > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(l));
        }
        BigInteger y = ModP(a * x + b);
        return (ulong)(y & ((1UL << l) - 1));
    }

    //func for 4-universal 
    static BigInteger Four_Universal_Hashing(BigInteger[] A, ulong x)
    {
        if (A.Length != 4) throw new ArgumentException("A must be length 4", nameof(A)); //check that A is of length 4
        BigInteger y = A[3];                //start with the last element of A
        for (int i = 2; i >= 0; i--)        
        {
            y = y * x + A[i];               
            y = (y & p) + (y >> 89);        //mod p

        }
        if (y >= p) y -= p;
        return y;                           
    }


    //func for creating the stream of keyvalue pairs 
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

    //func for calculating the square sum of the values in the hashtable for a given stream of keyvalue pairs. 
    static ulong SquareSumStream(Func<ulong, int, ulong> h, int l, IEnumerable<Tuple<ulong, int>> stream)
    {
        Chained_hashtable table = new Chained_hashtable(h, l);
        foreach (var (key, value) in stream)
        {
            table.increment(key, value);
        }
        return (ulong)table.SquareSumOfContents();
    }


    //function for retrieving random variables mod p.
    static BigInteger RandomModP()
    {
        BigInteger val;
        do
        {
            //get random bytes
            byte[] bytes = RandomNumberGenerator.GetBytes(12);
            val = ModP(BytesToUInt128(bytes));
        } while (val == 0);

        return val;
    }

    //main function for running the experiments and writing the results 
    static void Main()
    {
        //task c arrays
        int[] size_array = [20000, 100000, 400000, 1000000, 2000000];
        int[] l_array = [14, 14, 14, 14, 14];

        //declare variables
        IEnumerable<Tuple<ulong, int>> stream;

        DateTime start, end;           //variables for measuring time
        TimeSpan MS_time, MMP_time;    //variables for storing time results
        UInt128 MS_sum;                //variables for storing sum results
        UInt128 MMP_sum;               //variables for storing sum results
        TimeSpan[][] times = new TimeSpan[2][]; //array for storing time results
        for (int i = 0; i < 2; i++)
        {
            times[i] = new TimeSpan[5];     //initialize the times array to store the time results for each experiment
        }

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

            times[0][i] = end - start;

            start = DateTime.Now;
            foreach (var (key, value) in stream)
            {
                MMP_sum += multiplyModPrime(h2_a, h2_b, l_array[i], key);
            }
            end = DateTime.Now;
            MMP_time = end - start;
            times[1][i] = end - start;


            //writing to console
            Console.WriteLine($"Size of stream: {size_array[i]}   l size: {l_array[i]}\n");
            Console.WriteLine($"Multiply shift sum: {MS_sum}");
            Console.WriteLine($"Multiply mod prime sum: {MMP_sum}\n");
            Console.WriteLine($"Multiply shift time (ms): {MS_time.TotalMilliseconds}");
            Console.WriteLine($"Multiply mod prime time (ms): {MMP_time.TotalMilliseconds}\n\n");
        }

        //writing to file for R analysis
        using (var writer = new StreamWriter("taskC_times.csv"))
        {
            writer.WriteLine("time, n, l, type");
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    writer.WriteLine($"{times[i][j].TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},{size_array[j]},{l_array[j]},{(i == 0 ? "MS" : "MMP")}");
                }
            }
        }

        // Opgave 3 arrays
        int[] opgave3_size_array = [2000000, 2000000, 2000000, 2000000, 2000000];
        int[] opgave3_l_array = [12, 14, 16, 18, 20];

        //reset variables for task c
        for (int i = 0; i < opgave3_size_array.Length; i++)
        {
            stream = CreateStream(opgave3_size_array[i], opgave3_l_array[i]);

            start = DateTime.Now;
            // Rettet l_array[i] til opgave3_l_array[i]
            MS_sum = SquareSumStream((x, lValue) => multiplyShift(h1_a, lValue, x), opgave3_l_array[i], stream);
            end = DateTime.Now;
            MS_time = end - start;
            times[0][i] = end - start;

            start = DateTime.Now;
            // Rettet l_array[i] til opgave3_l_array[i]
            MMP_sum = SquareSumStream((x, lValue) => multiplyModPrime(h2_a, h2_b, lValue, x), opgave3_l_array[i], stream);
            end = DateTime.Now;
            MMP_time = end - start;
            times[1][i] = end - start;

            //writing to console
            Console.WriteLine($"Size of stream: {opgave3_size_array[i]}   l size: {opgave3_l_array[i]}");
            Console.WriteLine($"Multiply shift squared sum: {MS_sum}");
            Console.WriteLine($"Multiply mod prime squared sum: {MMP_sum}");
            Console.WriteLine($"Multiply shift time (ms): {MS_time.TotalMilliseconds}");
            Console.WriteLine($"Multiply mod prime time (ms): {MMP_time.TotalMilliseconds}\n");
        }

        //writing to file for R analysis

        using (var writer = new StreamWriter("opgave3.csv"))
        {
            writer.WriteLine("time, n, l, type");
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    writer.WriteLine($"{times[i][j].TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},{opgave3_size_array[j]},{opgave3_l_array[j]},{(i == 0 ? "MS" : "MMP")}");
                }
            }
        }


        //100 experiments
        int l = 16;
        stream = CreateStream(1000000, l);
        BigInteger a1, a2, a3, a4;
        ulong skecth_2nd_moment, exact_2nd_moment;
        exact_2nd_moment = SquareSumStream((x, l) => multiplyModPrime(h2_a, h2_b, l, x), l, stream);
        BigInteger[] a_array;
        BCS skecth;
        ulong[] experiments = new ulong[100];

        for (int i = 0; i < 100; i++)
        {
            a1 = RandomModP();
            a2 = RandomModP();
            a3 = RandomModP();
            a4 = RandomModP();
            a_array = new BigInteger[] { a1, a2, a3, a4 };
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
        ulong[] medians = new ulong[9];
        for (int i = 0; i < 9; i++)
        {
            ulong[] group = experiments.Skip(i * 11).Take(11).ToArray();
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


        (ulong, TimeSpan)[][] experiments_matrix = new (ulong, TimeSpan)[3][];
        ulong[][] medians_matrix = new ulong[3][];
        for (int i = 0; i < 3; i++)
        {
            experiments_matrix[i] = new (ulong, TimeSpan)[100];
            medians_matrix[i] = new ulong[9];
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
                a_array = new BigInteger[] { a1, a2, a3, a4 };
                start = DateTime.Now;
                skecth = new BCS(l_array[i], a_array, Four_Universal_Hashing);
                skecth.Process_stream(stream);
                skecth_2nd_moment = skecth.BCS_2nd_Moment();
                end = DateTime.Now;
                experiments_matrix[i][j] = (skecth_2nd_moment, end - start);
            }
        }

        //finding sorted version for each m size 
        (ulong, TimeSpan)[][] sorted_matrix = new (ulong, TimeSpan)[3][];
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
                ulong[] group = experiments_matrix[i].Skip(j * 11).Take(11).Select(x => x.Item1).ToArray();
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
