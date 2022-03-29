// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Demos.WcfService.Client.Demos.WcfService;
using Datadog.Util;

namespace Datadog.Demos.WcfService.Client
{
    public class Program
    {
        public static void Main()
        {
            PrintEnvironmentInformation();

            Console.WriteLine();
            Console.WriteLine($"----------- {typeof(Program).FullName} ----------- ");
            StringProviderClient client = new StringProviderClient();

            Console.WriteLine();
            Console.WriteLine("Calling GenerateRandomAsciiString(30)");
            string strVal = client.GenerateRandomAsciiString(30);

            Console.WriteLine();
            Console.WriteLine($"StringValue: \"{strVal}\".");

            Console.WriteLine();
            Console.WriteLine("Calling ComputeStableHash(..)");
            int hash = client.ComputeStableHash(strVal);

            Console.WriteLine();
            Console.WriteLine($"Hash:        {hash}.");

            Console.WriteLine();
            Console.WriteLine("-----------");

            Console.WriteLine();
            Console.WriteLine("Calling GenerateRandomAsciiStringWithHash(30)");
            StringInfo strInfo = client.GenerateRandomAsciiStringWithHash(30);

            Console.WriteLine();
            Console.WriteLine($"StringValue: \"{strInfo.StringValue}\".");
            Console.WriteLine($"Hash:        {strInfo.HashCode}.");

            Console.WriteLine();
            Console.WriteLine("Calling ComputeStableHash(..)");
            hash = client.ComputeStableHash(strInfo.StringValue);

            Console.WriteLine();
            Console.WriteLine($"Hash:        {hash}.");

            Console.WriteLine();
            Console.WriteLine("\nPress <Enter> to terminate the client.");
            Console.ReadLine();
            client.Close();
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
