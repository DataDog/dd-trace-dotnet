using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
            var bytes = new byte[] { 1, 5, 6 };
#pragma warning disable SYSLIB0021 // Type or member is obsolete
            // Vulnerable section
            MD5.Create().ComputeHash(new byte[] { 2, 5, 6 });
            Console.WriteLine("LINE ");
            testMethod();
            HashAlgorithm t = MD5.Create();
            t.ComputeHash(new byte[] { 4, 5, 6 });
            Console.WriteLine("LINE ");
            SHA1.Create().ComputeHash(new byte[] { 5, 5, 6 });
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

    private static void testMethod()
    {
        ((HashAlgorithm)(MD5.Create())).ComputeHash(new byte[] { 63, 5, 6 });
        Console.WriteLine("LINE ");
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

    private static void PrintCode()
    {
        string code = "";
        Assembly currentAssem = Assembly.GetExecutingAssembly();
        var assembly = AssemblyDefinition.ReadAssembly(currentAssem.Location);
        var types = assembly.MainModule.Types.Where(o => o.IsClass);
        var methods = types.SelectMany(type => type.Methods);

        foreach (var method in methods)
        {
            if ((method.Name == "Main") || (method.Name == "testHashAlgorithm") || (method.Name == "testMethod"))
            {
                code += "METHOD: method.Name" + Environment.NewLine;
                var instructions = method.Body?.Instructions;
                if (instructions != null)
                {
                    foreach (var instruction in instructions)
                    {
                        code += instruction.ToString() + Environment.NewLine;
                    }
                }
            }
        }

        Console.WriteLine(code);
    }
}
