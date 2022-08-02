// <copyright file="DebuggerSampleProcessHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger
{
    internal class DebuggerSampleProcessHelper : ProcessHelper
    {
        private const string StopSuffix = "stop";

        private readonly string _stopUrl;
        private readonly string _runUrl;

        public DebuggerSampleProcessHelper(string baseUrl, Process process, Action<string> onDataReceived = null)
            : base(process, onDataReceived)
        {
            _stopUrl = $"{baseUrl}{StopSuffix}";
            _runUrl = baseUrl;
        }

        internal async Task StopSample()
        {
            using var httpWebResponse = await WebRequest.CreateHttp(_stopUrl).GetResponseAsync();
            var timeout = 10_000;
            var isExited = Process.WaitForExit(timeout);

            if (!isExited)
            {
                throw new InvalidOperationException($"The process did not exit after {timeout}ms");
            }

            if (Process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Process exit code is {Process.ExitCode}");
            }
        }

        internal async Task RunCodeSample()
        {
            using var httpWebResponse = await WebRequest.CreateHttp(_runUrl).GetResponseAsync();
        }
    }
}
