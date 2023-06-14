// <copyright file="ServerlessMiniAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal static class ServerlessMiniAgent
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServerlessMiniAgent));

    internal static string GetMiniAgentPath(PlatformID os, ImmutableTracerSettings settings)
    {
        if (!settings.IsRunningInGCPFunctions && !settings.IsRunningInAzureFunctions)
        {
            return null;
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
                return null;
            }

            var isWindows = os == PlatformID.Win32NT;
            var dirPathSep = isWindows ? "\\" : "/";

            string rustBinaryPathRoot;
            if (settings.IsRunningInGCPFunctions)
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
            string rustBinaryFileExtension = isWindows ? ".exe" : string.Empty;
            rustBinaryPath = string.Format("{0}{1}{2}{3}datadog-serverless-trace-mini-agent{4}", rustBinaryPathRoot, dirPathSep, rustBinaryPathOsFolder, dirPathSep, rustBinaryFileExtension);
        }

        return rustBinaryPath;
    }

    internal static Process StartServerlessMiniAgent(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Log.Error("Can't spawn Serverless Mini Agent with null or empty path.");
            return null;
        }

        try
        {
            Log.Debug("Trying to spawn the Serverless Mini Agent at path: {Path}", path);
            Process process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += new DataReceivedEventHandler(MiniAgentDataReceivedHandler);

            process.Start();
            process.BeginOutputReadLine();
            return process;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error spawning the Serverless Mini Agent.");
            return null;
        }
    }

    // Tries to clean Mini Agent logs and log to the correct level, otherwise just logs the data as-is to Info
    // Mini Agent logs will be prefixed with "[Datadog Serverless Mini Agent]"
    private static void MiniAgentDataReceivedHandler(object sender, DataReceivedEventArgs outLine)
    {
        var data = outLine.Data;
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        var logTuple = ProcessMiniAgentLog(data);
        string level = logTuple.Item1;
        string processedLog = logTuple.Item2;

        switch (level)
        {
            case "ERROR":
                Log.Error("[Datadog Serverless Mini Agent] {Data}", processedLog);
                break;
            case "WARN":
                Log.Warning("[Datadog Serverless Mini Agent] {Data}", processedLog);
                break;
            case "INFO":
                Log.Information("[Datadog Serverless Mini Agent] {Data}", processedLog);
                break;
            case "DEBUG":
                Log.Debug("[Datadog Serverless Mini Agent] {Data}", processedLog);
                break;
            default:
                Log.Information("[Datadog Serverless Mini Agent] {Data}", data);
                break;
        }
    }

    // Processes a raw log from the mini agent, returning a Tuple of the log level and the cleaned log data
    // For example, given this raw log:
    // [2023-06-06T01:31:30Z DEBUG datadog_trace_mini_agent::mini_agent] Random log
    // This function will return:
    // ("DEBUG", "Random log")
    internal static Tuple<string, string> ProcessMiniAgentLog(string rawLog)
    {
        int logPrefixCutoff = rawLog.IndexOf("]");
        if (logPrefixCutoff < 0 || logPrefixCutoff == rawLog.Length - 1)
        {
            return Tuple.Create("INFO", rawLog);
        }

        string level = rawLog.Substring(0, logPrefixCutoff).Split(' ')[1];
        if (!(level is "ERROR" or "WARN" or "INFO" or "DEBUG"))
        {
            return Tuple.Create("INFO", rawLog);
        }

        return Tuple.Create(level, rawLog.Substring(logPrefixCutoff + 1).Trim());
    }
}
