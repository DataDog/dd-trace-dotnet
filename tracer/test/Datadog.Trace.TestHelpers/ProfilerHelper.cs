// <copyright file="ProfilerHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;

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
    }
}
