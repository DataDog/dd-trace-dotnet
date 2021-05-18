// <copyright file="ProcessHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// Drains the standard and error output of a process
    /// </summary>
    internal class ProcessHelper : IDisposable
    {
        private readonly ManualResetEventSlim _errorMutex = new();
        private readonly ManualResetEventSlim _outputMutex = new();
        private readonly StringBuilder _outputBuffer = new();
        private readonly StringBuilder _errorBuffer = new();

        public ProcessHelper(Process process)
        {
            process.OutputDataReceived += (_, e) => DrainOutput(e, _outputBuffer, _outputMutex);
            process.ErrorDataReceived += (_, e) => DrainOutput(e, _errorBuffer, _errorMutex);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public string StandardOutput => _outputBuffer.ToString();

        public string ErrorOutput => _errorBuffer.ToString();

        public bool Drain(int timeout = Timeout.Infinite)
        {
            if (timeout != Timeout.Infinite)
            {
                timeout /= 2;
            }

            return _outputMutex.Wait(timeout) && _errorMutex.Wait(timeout);
        }

        public void Dispose()
        {
            _errorMutex.Dispose();
            _outputMutex.Dispose();
        }

        private static void DrainOutput(DataReceivedEventArgs e, StringBuilder buffer, ManualResetEventSlim mutex)
        {
            if (e.Data == null)
            {
                mutex.Set();
            }
            else
            {
                buffer.AppendLine(e.Data);
            }
        }
    }
}
