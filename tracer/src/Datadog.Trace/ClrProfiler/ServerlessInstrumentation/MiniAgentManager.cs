// <copyright file="MiniAgentManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class MiniAgentManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MiniAgentManager));

    internal virtual void Start(string path)
    {
        Process process = new Process();
        process.StartInfo.FileName = path;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                LogMiniAgentToCorrectLevel(e.Data);
            }
        });
        process.Start();
        process.BeginOutputReadLine();
    }

    private static void LogMiniAgentToCorrectLevel(string data)
    {
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
