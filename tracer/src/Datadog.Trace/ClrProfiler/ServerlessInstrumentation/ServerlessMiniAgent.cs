// <copyright file="ServerlessMiniAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class ServerlessMiniAgent
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServerlessMiniAgent));
    internal const string AzureFunctionNameEnvVar = "WEBSITE_SITE_NAME";
    internal const string AzureFunctionIdentifierEnvVar = "FUNCTIONS_EXTENSION_VERSION";
    internal const string GCPFunctionDeprecatedNameEnvVar = "FUNCTION_NAME";
    internal const string GCPFunctionDeprecatedEnvVarIdentifier = "GCP_PROJECT";
    internal const string GCPFunctionNewerNameEnvVar = "K_SERVICE";
    internal const string GCPFunctionNewerEnvVarIdentifier = "FUNCTION_TARGET";

    internal static bool IsGCPFunction { get; private set; } = GetIsGCPFunction();

    internal static bool IsAzureFunction { get; private set; } = GetIsAzureFunction();

    internal static void MaybeStartMiniAgent(ServerlessMiniAgentManager manager)
    {
        if (!IsGCPFunction && !IsAzureFunction)
        {
            return;
        }

        Log.Information("Trying to start the mini agent");

        string rustBinaryPath;
        if (Environment.GetEnvironmentVariable("DD_MINI_AGENT_PATH") != null)
        {
            rustBinaryPath = Environment.GetEnvironmentVariable("DD_MINI_AGENT_PATH");
        }
        else
        {
            // Environment.OSVersion.Platform can return PlatformID.Unix on MacOS, this is OK as GCP & Azure don't have MacOs functions.
            if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Log.Error("Serverless Mini Agent is only supported on Windows and Linux.");
                return;
            }

            var dirPathSep = Path.DirectorySeparatorChar;
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

            string rustBinaryPathRoot;
            if (IsGCPFunction)
            {
                rustBinaryPathRoot = "/layers/google.dotnet.publish/publish/bin";
            }
            else
            {
                // IsAzureFunction
                if (isWindows)
                {
                    rustBinaryPathRoot = "C:\\home\\site\\wwwroot";
                }
                else
                {
                    // linux
                    rustBinaryPathRoot = "/home/site/wwwroot";
                }
            }

            string rustBinaryPathOsFolder = isWindows ? "datadog-serverless-agent-windows-amd64" : "datadog-serverless-agent-linux-amd64";
            rustBinaryPath = string.Format("{0}{1}{2}{3}datadog-serverless-trace-mini-agent", rustBinaryPathRoot, dirPathSep, rustBinaryPathOsFolder, dirPathSep);
        }

        manager.Start(rustBinaryPath);
    }

    internal static bool GetIsGCPFunction()
    {
        bool isDeprecatedGCPFunction = Environment.GetEnvironmentVariable(GCPFunctionDeprecatedNameEnvVar) != null && Environment.GetEnvironmentVariable(GCPFunctionDeprecatedEnvVarIdentifier) != null;
        bool isNewerGCPFunction = Environment.GetEnvironmentVariable(GCPFunctionNewerNameEnvVar) != null && Environment.GetEnvironmentVariable(GCPFunctionNewerEnvVarIdentifier) != null;

        return isDeprecatedGCPFunction || isNewerGCPFunction;
    }

    internal static bool GetIsAzureFunction()
    {
        return Environment.GetEnvironmentVariable(AzureFunctionIdentifierEnvVar) != null && Environment.GetEnvironmentVariable(AzureFunctionNameEnvVar) != null;
    }

    // Used for unit tests
    internal static void UpdateIsGCPAzureEnvVarsTestsOnly()
    {
        IsAzureFunction = GetIsAzureFunction();
        IsGCPFunction = GetIsGCPFunction();
    }

    internal static string GetGCPAzureFunctionName()
    {
        if (IsAzureFunction)
        {
            return Environment.GetEnvironmentVariable(AzureFunctionNameEnvVar);
        }

        if (IsGCPFunction)
        {
            return Environment.GetEnvironmentVariable(GCPFunctionDeprecatedNameEnvVar) ?? Environment.GetEnvironmentVariable(GCPFunctionNewerNameEnvVar);
        }

        return null;
    }
}
