// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Description;
using Datadog.Demos.WcfService.Library;
using Datadog.Util;

namespace Datadog.Demos.WcfService.Host
{
    public class Program
    {
        public static void Main()
        {
            PrintEnvironmentInformation();

            Console.WriteLine();
            Console.WriteLine($"----------- {typeof(Program).FullName} ----------- ");

            // Step 1: Create a URI to serve as the base address.
            Uri baseHttpAddress = new Uri("http://localhost:8000/Datadog.Demos.WcfService/");
            Uri baseHttpsAddress = new Uri("https://localhost:44310/Datadog.Demos.WcfService/");

            // Step 2: Create a ServiceHost instance.
            ServiceHost selfHost = new ServiceHost(typeof(StringProvider), baseHttpAddress, baseHttpsAddress);

            try
            {
                // Step 3: Add a service endpoint.
                selfHost.AddServiceEndpoint(typeof(IStringProvider), new BasicHttpBinding(), "StringProvider");
                selfHost.AddServiceEndpoint(typeof(IStringProvider), new BasicHttpsBinding(), "StringProvider");

                // Step 4: Enable metadata exchange.
                ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                smb.HttpsGetEnabled = true;
                selfHost.Description.Behaviors.Add(smb);

                // Step 5: Start the service.
                selfHost.Open();
                Console.WriteLine("The service is ready.");

                // Close the ServiceHost to stop the service.
                Console.WriteLine("Press <Enter> to terminate the service.");
                Console.WriteLine();
                Console.ReadLine();
                selfHost.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                selfHost.Abort();
            }
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
