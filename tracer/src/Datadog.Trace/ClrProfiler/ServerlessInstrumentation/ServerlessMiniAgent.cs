// <copyright file="ServerlessMiniAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal static class ServerlessMiniAgent
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServerlessMiniAgent));

    internal static string GetMiniAgentPath(PlatformID os, ImmutableTracerSettings settings)
    {
        if (!settings.IsRunningInGCPFunctions && !settings.IsRunningMiniAgentInAzureFunctions)
        {
            return null;
        }

        if (EnvironmentHelpers.GetEnvironmentVariable("DD_MINI_AGENT_PATH") is { } path)
        {
            return path;
        }

        // Environment.OSVersion.Platform can return PlatformID.Unix on MacOS, this is OK as GCP & Azure don't have MacOs functions.
        if (os != PlatformID.Unix && os != PlatformID.Win32NT)
        {
            Log.Error("Serverless Mini Agent is only supported on Windows and Linux.");
            return null;
        }

        string rustBinaryPathRoot;
        if (settings.IsRunningInGCPFunctions)
        {
            rustBinaryPathRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "layers", "google.dotnet.publish", "publish", "bin");
        }
        else
        {
            rustBinaryPathRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "home", "site", "wwwroot");
        }

        var isWindows = os == PlatformID.Win32NT;

        string rustBinaryPathOsFolder = isWindows ? "datadog-serverless-agent-windows-amd64" : "datadog-serverless-agent-linux-amd64";
        string rustBinaryName = isWindows ? "datadog-serverless-trace-mini-agent.exe" : "datadog-serverless-trace-mini-agent";
        return Path.Combine(rustBinaryPathRoot, rustBinaryPathOsFolder, rustBinaryName);
    }

    internal static void StartServerlessMiniAgent(ImmutableTracerSettings settings)
    {
        var serverlessMiniAgentPath = ServerlessMiniAgent.GetMiniAgentPath(Environment.OSVersion.Platform, settings);
        if (string.IsNullOrEmpty(serverlessMiniAgentPath))
        {
            return;
        }

        if (!File.Exists(serverlessMiniAgentPath))
        {
            Log.Error("Serverless Mini Agent does not exist: {Path}", serverlessMiniAgentPath);
            return;
        }

        try
        {
            Log.Debug("Trying to spawn the Serverless Mini Agent at path: {Path}", serverlessMiniAgentPath);
            Process process = new Process();
            process.StartInfo.FileName = serverlessMiniAgentPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += MiniAgentDataReceivedHandler;

            process.Start();
            process.BeginOutputReadLine();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error spawning the Serverless Mini Agent.");
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

        var level = "INFO";
        var log = data;
        ProcessMiniAgentLog(data, out level, out log);

        switch (level)
        {
            case "ERROR":
                Log.Error("[Datadog Serverless Mini Agent] {Data}", log);
                break;
            case "WARN":
                Log.Warning("[Datadog Serverless Mini Agent] {Data}", log);
                break;
            case "INFO":
                Log.Information("[Datadog Serverless Mini Agent] {Data}", log);
                break;
            case "DEBUG":
                Log.Debug("[Datadog Serverless Mini Agent] {Data}", log);
                break;
            default:
                Log.Information("[Datadog Serverless Mini Agent] {Data}", data);
                break;
        }
    }

    // Processes a raw log from the mini agent, modifying two "out" parameters level and log.
    // For example, given this raw log:
    // [2023-06-06T01:31:30Z DEBUG datadog_trace_mini_agent::mini_agent] Random log
    // level and log will be the following values:
    // level == "DEBUG", log == "Random log"
    internal static void ProcessMiniAgentLog(string rawLog, out string level, out string log)
    {
        level = "INFO";
        log = rawLog;

        int logPrefixCutoff = rawLog.IndexOf("]");
        if (logPrefixCutoff < 0 || logPrefixCutoff == rawLog.Length - 1)
        {
            return;
        }

        int levelLeftBound = rawLog.IndexOf(" ");
        if (levelLeftBound < 0)
        {
            return;
        }

        int levelRightBound = rawLog.IndexOf(" ", levelLeftBound + 1);
        if (levelRightBound < 0 || levelRightBound - levelLeftBound < 1)
        {
            return;
        }

        string parsedLevel = rawLog.Substring(levelLeftBound + 1, levelRightBound - levelLeftBound - 1);

        if (!(parsedLevel is "ERROR" or "WARN" or "INFO" or "DEBUG"))
        {
            return;
        }

        level = parsedLevel;
        log = rawLog.Substring(logPrefixCutoff + 2);

        if (level is "DEBUG")
        {
            level = "INFO";
            log = $"[DEBUG] {log}";
        }
    }
}
