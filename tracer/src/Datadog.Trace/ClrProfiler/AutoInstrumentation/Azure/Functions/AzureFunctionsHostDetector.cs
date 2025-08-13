// <copyright file="AzureFunctionsHostDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.Logging;

internal static class AzureFunctionsHostDetector
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionsHostDetector));

    public static readonly bool IsRunningInFunctionsHost;

    static AzureFunctionsHostDetector()
    {
        IsRunningInFunctionsHost = DetectIfFunctionsHost();

        if (IsRunningInFunctionsHost)
        {
            Log.Information("Detected Azure Functions host process.");
        }
    }

    private static bool DetectIfFunctionsHost()
    {
        try
        {
            var functionsRuntime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
            if (string.IsNullOrEmpty(functionsRuntime))
            {
                return false; // we are the worker process, not the host
            }

            // Check if we are isolated
            if (!string.Equals(functionsRuntime, "dotnet-isolated", StringComparison.OrdinalIgnoreCase))
            {
                return false; // yeah azure functions, but not isolated, so not an issue here
            }

            // at this point we can assume that we are in Azure Functions
            // granted this may not always be the case though as we've basically just checked the environment variable

            var workerId = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_ID");
            if (!string.IsNullOrEmpty(workerId))
            {
                // We're in the worker process, not the host
                return false;
            }

            // at this point we can assume that we are in the host process
            // but again this is just us checking the environment variable(s)
            // Alternatively, we can check the process name and loaded assemblies if we want to be even more thorough
            // like so below, but unsure if it is worth it as
            // this is still guarded by having another env var set to disable ILogger
            var process = Process.GetCurrentProcess();
            var processName = process.ProcessName.ToLowerInvariant();
            var isKnownHostProcess = processName == "func" || processName.Contains("microsoft.azure.webjobs.script.webhost") || processName == "webhost";

            return isKnownHostProcess;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting Azure Functions host process");
            return false; // just assume we aren't the host
        }
    }
}
