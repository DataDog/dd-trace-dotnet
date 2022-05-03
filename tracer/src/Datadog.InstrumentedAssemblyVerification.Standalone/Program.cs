using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Datadog.InstrumentedAssemblyGenerator;

namespace Datadog.InstrumentedAssemblyVerification.Standalone
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                SetSize();
                PrintWelcome();

                var (output, modulesToVerify) = Parse(args);
                var generatorArgs = new AssemblyGeneratorArgs(output, modulesToVerify);

                var exportedModulesPathsAndMethods = InstrumentedAssemblyGeneration.Generate(generatorArgs);

                var results = new List<VerificationOutcome>();
                foreach (var (modulePath, methods) in exportedModulesPathsAndMethods)
                {
                    string moduleName = Path.GetFileName(modulePath);
                    string originalModulePath = Path.Combine(generatorArgs.OriginalModulesFolder, moduleName);
                    var result = new VerificationsRunner(modulePath, originalModulePath, methods).Run();
                    results.Add(result);
                }

                PrintResultMessage(results);

                return results.Any(r => !r.IsValid) ? -1 : 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return -1;
            }
        }

        private static void PrintResultMessage(List<VerificationOutcome> results)
        {
            var currentConsoleColor = Console.ForegroundColor;
            if (results.Any(r => !r.IsValid))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Join(Environment.NewLine, results.Select(r => r.FailureReason)));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Finished without errors");
            }
            Console.ForegroundColor = currentConsoleColor;
        }

        private static (string, string[]) Parse(string[] args)
        {
            if (args.Length < 2 ||
                string.IsNullOrWhiteSpace(args[0]) ||
                string.IsNullOrWhiteSpace(args[1]))
            {
                PrintHelp(args);
                throw new ArgumentException();
            }

            return (args[0], args[1].Split(',').Select(mod => mod.Trim()).ToArray());
        }

        private static void PrintHelp(string[] args)
        {
            string helpMesg = "Usage: Datadog.InstrumentedAssemblyVerification.Standalone.exe [instrumented assembly logs folder] [module to generate1, module to generate2]";
            Console.WriteLine("--Help");
            Console.WriteLine($"| {helpMesg} |");
            Console.WriteLine();

            if (args.Length > 0 && args.Any(a => !string.IsNullOrEmpty(a)))
            {
                Console.WriteLine("\r\nProvided Arguments:");
            }

            foreach (string s in args)
            {
                Console.WriteLine($"   {s}");
            }
        }

        private static void SetSize()
        {
            try
            {
                ShowWindow(GetConsoleWindow(), Maximize);
            }
            catch
            {
                // ignored
            }
        }

        private static void PrintWelcome()
        {
            string version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
            Console.WriteLine("\t---------------------------------------------------------------");
            Console.WriteLine($"\t Welcome to InstrumentedAssemblyVerification (version {version})");
            Console.WriteLine("\t---------------------------------------------------------------\r\n");
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int Maximize = 3;
    }
}