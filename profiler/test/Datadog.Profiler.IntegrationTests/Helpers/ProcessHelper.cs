// <copyright file="ProcessHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

/*
 * This file was copied from the Tracer repository.
 * Once the profiler is merged in the Tracer repository, we should reuse their class
 */

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Datadog.Profiler.IntegrationTests.Helpers
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
        private readonly Process _process;

        public ProcessHelper(Process process)
        {
            _process = process;
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Process = process;
        }

        public Process Process { get; }

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

            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnErrorDataReceived;
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

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            DrainOutput(e, _errorBuffer, _errorMutex);
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            DrainOutput(e, _outputBuffer, _outputMutex);
        }
    }
}
