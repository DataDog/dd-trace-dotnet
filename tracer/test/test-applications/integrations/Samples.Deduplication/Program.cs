using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Deduplication;

internal static class Program
{
    private static void Main(string[] args)
    {
        ComputeHashNTimes(GetExecutionTimes(args));
    }

    private static void ComputeHashNTimes(int times)
    {
        for (int i = 0; i < times; i++)
        {
            var bytes = new byte[] { 3, 5, 6 };
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            // Vulnerable section
            MD5.Create().ComputeHash(new byte[] { 3, 5, 6 });
            Console.WriteLine("LINE ");
            ((HashAlgorithm)(MD5.Create())).ComputeHash(new byte[] { 3, 5, 6 });
            Console.WriteLine("LINE ");
            HashAlgorithm t = MD5.Create();
            t.ComputeHash(new byte[] { 3, 5, 6 });
            Console.WriteLine("LINE ");
            SHA1.Create().ComputeHash(new byte[] { 3, 5, 6 });
            Console.WriteLine("LINE ");
            MD5.Create().ComputeHash(bytes);
            Console.WriteLine("LINE ");
            SHA1.Create().ComputeHash(bytes);
            Console.WriteLine("LINE ");
            testHashAlgorithm(SHA1.Create());
            Console.WriteLine("LINE ");
            testHashAlgorithm(MD5.Create());
            Console.WriteLine("LINE ");
#pragma warning restore SYSLIB0021 // Type or member is obsolete
        }

        testHashAlgorithm(new SHA1CryptoServiceProvider());
        Console.WriteLine("MAIN DUPLICATED OUT");
    }

    private static int GetExecutionTimes(string[] args)
    {
        if (args == null || args.Length != 1 || args[0] == null)
        {
            return 1;
        }

        int times;

        try
        {
            times = Convert.ToInt32(args[0]);
        }
        catch
        {
            times = 1;
        }

        return times;
    }

    private static void testHashAlgorithm(HashAlgorithm algorithm)
    {
        var byteArg = new byte[] { 3, 5, 6 };
        algorithm.ComputeHash(byteArg);
    }
}
