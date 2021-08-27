// <copyright file="ProfilerHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class ProfilerHelper
    {
        public static Process StartProcessWithProfiler(
            string executable,
            EnvironmentHelper environmentHelper,
            string arguments = null,
            bool redirectStandardInput = false,
            int traceAgentPort = 9696,
            int aspNetCorePort = 5000,
            int? statsdPort = null,
            string processToProfile = null,
            bool? enableSecurity = null,
            bool? enableBlocking = null,
            bool? callTargetEnabled = null)
        {
            if (environmentHelper == null)
            {
                throw new ArgumentNullException(nameof(environmentHelper));
            }

            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            var startInfo = new ProcessStartInfo(executable, $"{arguments ?? string.Empty}");

            environmentHelper.SetEnvironmentVariables(traceAgentPort, aspNetCorePort, statsdPort, startInfo.EnvironmentVariables, processToProfile, enableSecurity.GetValueOrDefault(), enableBlocking.GetValueOrDefault(), callTargetEnabled.GetValueOrDefault());

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;

            return Process.Start(startInfo);
        }

        public static Process StartFunctionsHostWithProfiler(
            string projectDirectory,
            EnvironmentHelper environmentHelper,
            ITestOutputHelper output,
            string arguments = null,
            bool redirectStandardInput = false,
            int traceAgentPort = 9696,
            int functionsHostPort = 5000,
            int? statsdPort = null,
            string processToProfile = null)
        {
            if (environmentHelper == null)
            {
                throw new ArgumentNullException(nameof(environmentHelper));
            }

            // clear all relevant environment variables to start with a clean slate
            EnvironmentHelper.ClearProfilerEnvironmentVariables();

            var funcStartCmd = $"/C func start --script-root {projectDirectory} --port {functionsHostPort}";
            Console.WriteLine(funcStartCmd);

            var startInfo = new ProcessStartInfo(fileName: "cmd.exe", arguments: funcStartCmd);
            environmentHelper.SetEnvironmentVariables(traceAgentPort, functionsHostPort, statsdPort, startInfo.EnvironmentVariables, processToProfile);

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = redirectStandardInput;

            var process = Process.Start(startInfo);

            if (process != null)
            {
                process.OutputDataReceived += (sender, args) => output.WriteLine(args.Data);
                process.ErrorDataReceived += (sender, args) => output.WriteLine(args.Data);
            }

            var maxWait = 25_000;
            while (!PortInUse(functionsHostPort))
            {
                Thread.Sleep(50);

                if (process == null)
                {
                    throw new Exception("Functions host process instance is null");
                }

                if (process.HasExited)
                {
                    throw new Exception("Functions host process has already exited");
                }

                maxWait -= 50;
                if (maxWait <= 0)
                {
                    throw new Exception("Unable to verify functions host start");
                }
            }

            output.WriteLine($"Verified that a process has bound to port: {functionsHostPort}");

            return process;
        }

        public static bool PortInUse(int port)
        {
            var inUse = false;

            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            }

            return inUse;
        }
    }
}
