// <copyright file="DebuggerSampleProcessHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Debugger.IntegrationTests.Helpers
{
    internal class DebuggerSampleProcessHelper : ProcessHelper
    {
        private const string StopSuffix = "stop";

        private readonly string _stopUrl;
        private readonly string _runUrl;
        private readonly ITestOutputHelper _output;

        public DebuggerSampleProcessHelper(string baseUrl, Process process, ITestOutputHelper output, Action<string> onDataReceived = null)
            : base(process, onDataReceived)
        {
            _stopUrl = $"{baseUrl}{StopSuffix}";
            _runUrl = baseUrl;
            _output = output;
        }

        internal async Task StopSample()
        {
            // The sample process may have already exited (e.g., crash or early termination).
            // In that case, calling /stop will fail with connection refused, masking the real cause.
            if (Process.HasExited)
            {
                _output.WriteLine($"[DebuggerSampleProcessHelper] Process already exited with code {Process.ExitCode} (0x{Process.ExitCode:X}). Skipping /stop request.");
                _output.WriteLine($"[DebuggerSampleProcessHelper] Standard output:\n{StandardOutput}");
                _output.WriteLine($"[DebuggerSampleProcessHelper] Error output:\n{ErrorOutput}");
                ExitCodeException.ThrowIfNonZero(Process.ExitCode);
                return;
            }

            try
            {
                using var httpWebResponse = await WebRequest.CreateHttp(_stopUrl).GetResponseAsync();
            }
            catch (WebException ex) when (ex.InnerException is HttpRequestException or SocketException)
            {
                // If the process exited between the HasExited check and the /stop request, surface exit details.
                if (Process.HasExited)
                {
                    _output.WriteLine($"[DebuggerSampleProcessHelper] /stop request failed because the process already exited with code {Process.ExitCode} (0x{Process.ExitCode:X}).");
                    _output.WriteLine($"[DebuggerSampleProcessHelper] Standard output:\n{StandardOutput}");
                    _output.WriteLine($"[DebuggerSampleProcessHelper] Error output:\n{ErrorOutput}");
                    ExitCodeException.ThrowIfNonZero(Process.ExitCode);
                    return;
                }

                throw;
            }

            var timeout = 10_000;
            var isExited = Process.WaitForExit(timeout);

            if (!isExited)
            {
                throw new InvalidOperationException($"The process did not exit after {timeout}ms");
            }

            if (Process.ExitCode != 0)
            {
                _output.WriteLine($"[DebuggerSampleProcessHelper] Process exited with code {Process.ExitCode} (0x{Process.ExitCode:X})");
                _output.WriteLine($"[DebuggerSampleProcessHelper] Standard output:\n{StandardOutput}");
                _output.WriteLine($"[DebuggerSampleProcessHelper] Error output:\n{ErrorOutput}");
            }

            ExitCodeException.ThrowIfNonZero(Process.ExitCode);
        }

        internal async Task RunCodeSample()
        {
            using var httpWebResponse = await WebRequest.CreateHttp(_runUrl).GetResponseAsync();
        }
    }
}
