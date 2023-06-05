// <copyright file="MiniAgentManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class MiniAgentManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MiniAgentManager));

    internal virtual void Start(string path)
    {
        try
        {
            Log.Debug("Trying to spawn the Serverless Mini Agent at path: {Path}", path);
            Process process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            var miniAgentLogsHandler = new DataReceivedEventHandler((sender, e) =>
            {
                ProcessMiniAgentLogData(e.Data);
            });

            process.OutputDataReceived += miniAgentLogsHandler;
            process.ErrorDataReceived += miniAgentLogsHandler;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error spawning the Serverless Mini Agent.");
        }
    }

    private static void ProcessMiniAgentLogData(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        string[] split = data.Split(' ');
        int logPrefixCutoff = data.IndexOf("]");
        if (split.Length < 1 || logPrefixCutoff < 0)
        {
            return;
        }

        string level = split[1];
        string processed = "[Datadog Serverless Mini Agent" + data.Substring(logPrefixCutoff);
        switch (level)
        {
            case "ERROR":
                Log.Error("{Data}", processed);
                break;
            case "WARN":
                Log.Warning("{Data}", processed);
                break;
            case "INFO":
                Log.Information("{Data}", processed);
                break;
            case "DEBUG":
                Log.Debug("{Data}", processed);
                break;
        }
    }
}
