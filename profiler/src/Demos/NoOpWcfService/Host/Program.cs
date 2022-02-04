// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using Datadog.Demos.NoOpWcfService.Library;
using Datadog.Util;

namespace Datadog.Demos.NoOpWcfService.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} [--timeout TimeoutInSeconds | --run-infinitely]");
            Console.WriteLine();
            Console.WriteLine(" ########### Starting.");

            PrintEnvironmentInformation();

            ParseCommandLine(args, out TimeSpan timeout);

            bool hasTimeout = (timeout > TimeSpan.Zero);
            if (hasTimeout)
            {
                Console.WriteLine($" ########### The application will run non-interactively for {timeout} and will stop after that time.");
            }
            else
            {
                Console.WriteLine($" ########### The application will run interactively because no timeout was specified or could be parsed.");
            }

            var server = new ServiceHost(typeof(LibraryService));
            server.Open();

            Console.WriteLine("Your service is started...");

            if (hasTimeout)
            {
                Thread.Sleep(timeout);
            }
            else
            {
                Console.WriteLine($"{Environment.NewLine} ########### Press enter to finish.");
                Console.ReadLine();
            }

            server.Close();

            Console.WriteLine($"{Environment.NewLine} ########### Server was shut down. Exiting.");
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout)
        {
            if (args.Length > 1 && "--timeout".Equals(args[0], StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(args[1], out var timeoutInSecond))
                {
                    timeout = TimeSpan.FromSeconds(timeoutInSecond);
                    return;
                }
            }

            if (args.Length > 0 && "--run-infinitely".Equals(args[0], StringComparison.OrdinalIgnoreCase))
            {
                timeout = Timeout.InfiniteTimeSpan;
                return;
            }

            timeout = TimeSpan.Zero;
            return;
        }

        private static void PrintEnvironmentInformation()
        {
            Process process = Process.GetCurrentProcess();

            Console.WriteLine("Environment info");
            Console.WriteLine("{ ");
            Console.WriteLine($"    Process Id:             {process.Id}");
            Console.WriteLine($"    Process Name:           {process.ProcessName}");
            Console.WriteLine($"    Current Working Dir:    {Environment.CurrentDirectory}");
            Console.WriteLine($"    Is64BitProcess:         {Environment.Is64BitProcess}");
            Console.WriteLine($"    Runtime version:        {Environment.Version}");
            Console.WriteLine($"    OS version:             {Environment.OSVersion}");
            Console.WriteLine($"    Common App Data folder: {Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}");
            Console.WriteLine();

            Console.WriteLine("    Variables:");
            Console.WriteLine();
            Console.WriteLine("        CORECLR_ENABLE_PROFILING:  " + (Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING") ?? "null"));
            Console.WriteLine("        CORECLR_PROFILER:          " + (Environment.GetEnvironmentVariable("CORECLR_PROFILER") ?? "null"));
            Console.WriteLine("        CORECLR_PROFILER_PATH_64:  " + (Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH_64") ?? "null"));
            Console.WriteLine("        CORECLR_PROFILER_PATH_32:  " + (Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH_32") ?? "null"));
            Console.WriteLine("        CORECLR_PROFILER_PATH:     " + (Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH") ?? "null"));
            Console.WriteLine();
            Console.WriteLine("        COR_ENABLE_PROFILING:      " + (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") ?? "null"));
            Console.WriteLine("        COR_PROFILER:              " + (Environment.GetEnvironmentVariable("COR_PROFILER") ?? "null"));
            Console.WriteLine("        COR_PROFILER_PATH_64:      " + (Environment.GetEnvironmentVariable("COR_PROFILER_PATH_64") ?? "null"));
            Console.WriteLine("        COR_PROFILER_PATH_32:      " + (Environment.GetEnvironmentVariable("COR_PROFILER_PATH_32") ?? "null"));
            Console.WriteLine("        COR_PROFILER_PATH:         " + (Environment.GetEnvironmentVariable("COR_PROFILER_PATH") ?? "null"));
            Console.WriteLine();
            Console.WriteLine("        DD_DOTNET_PROFILER_HOME:   " + (Environment.GetEnvironmentVariable("DD_DOTNET_PROFILER_HOME") ?? "null"));
            Console.WriteLine();
            Console.WriteLine("        COMPlus_EnableDiagnostics: " + (Environment.GetEnvironmentVariable("COMPlus_EnableDiagnostics") ?? "null"));
            Console.WriteLine();

            Console.WriteLine("    RuntimeEnvironmentInfo:");
            Console.WriteLine();
            Console.WriteLine("        " + RuntimeEnvironmentInfo.SingeltonInstance.ToString());

            Console.WriteLine();
            Console.WriteLine("    AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName:");
            Console.WriteLine("        \"" + (AppDomain.CurrentDomain?.SetupInformation?.TargetFrameworkName ?? "<NULL>") + "\"");

            Console.WriteLine("} ");
            Console.WriteLine();
        }
    }
}
