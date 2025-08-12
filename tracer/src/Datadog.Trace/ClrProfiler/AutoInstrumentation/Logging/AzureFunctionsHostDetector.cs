// <copyright file="AzureFunctionsHostDetector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
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
            Log.Information("Detected Azure Functions host process. Direct log submission will be disabled for ILogger to prevent duplicate logs.");
        }
    }

    private static bool DetectIfFunctionsHost()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var processName = process.ProcessName.ToLowerInvariant();

            // Check if we're in a known Azure Functions host process
            if (processName == "func" ||
                processName.Contains("microsoft.azure.webjobs.script"))
            {
                var functionsRuntime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
                if (string.IsNullOrEmpty(functionsRuntime))
                {
                    return false; // Not Azure Functions
                }

                // The host process won't have this, but the worker will
                var workerId = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_ID");
                if (!string.IsNullOrEmpty(workerId))
                {
                    // We're in the worker process, not the host
                    return false;
                }

                // function host will have these loaded (I think)
                var hasHostAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.GetName().Name == "Microsoft.Azure.WebJobs.Script.WebHost" ||
                              a.GetName().Name == "Microsoft.Azure.WebJobs.Script");

                return hasHostAssemblies;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting Azure Functions host process");
            return false;
        }
    }
}
