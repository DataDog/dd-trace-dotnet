// <copyright file="ProcessHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Drains the standard and error output of a process
    /// </summary>
    public class ProcessHelper : IDisposable
    {
        private readonly TaskCompletionSource<bool> _errorTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _outputTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly StringBuilder _outputBuffer = new();
        private readonly StringBuilder _errorBuffer = new();

        public ProcessHelper(Process process, Action<string> onDataReceived = null, Action<string> onErrorReceived = null)
        {
            Task = Task.WhenAll(_outputTask.Task, _errorTask.Task);

            Task.Factory.StartNew(() => DrainOutput(process.StandardOutput, _outputBuffer, _outputTask, onDataReceived), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => DrainOutput(process.StandardError, _errorBuffer, _errorTask, onErrorReceived ?? onDataReceived), TaskCreationOptions.LongRunning);

            Process = process;
        }

        public Process Process { get; }

        public string StandardOutput => _outputBuffer.ToString();

        public string ErrorOutput => _errorBuffer.ToString();

        public Task Task { get; }

        public bool Drain(int timeout = Timeout.Infinite)
        {
            if (timeout != Timeout.Infinite)
            {
                timeout /= 2;
            }

            return _outputTask.Task.Wait(timeout) && _errorTask.Task.Wait(timeout);
        }

        public virtual void Dispose()
        {
            if (!Process.HasExited)
            {
                Process.Kill();
            }
        }

        private void DrainOutput(StreamReader stream, StringBuilder buffer, TaskCompletionSource<bool> tcs, Action<string> onDataReceived)
        {
            while (stream.ReadLine() is { } line)
            {
                buffer.AppendLine(line);

                try
                {
                    onDataReceived?.Invoke(line);
                }
                catch (Exception)
                {
                }
            }

            tcs.TrySetResult(true);
        }
    }
}
