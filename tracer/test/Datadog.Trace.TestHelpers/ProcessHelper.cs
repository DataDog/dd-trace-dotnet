// <copyright file="ProcessHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Drains the standard and error output of a process in a deadlock-free way.
    /// </summary>
    public partial class ProcessHelper : IDisposable
    {
        private readonly TaskCompletionSource<bool> _errorTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _outputTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly StringBuilder _outputBuffer = new();
        private readonly StringBuilder _errorBuffer = new();
        private readonly ReadOnlyDictionary<string, string> _environmentVariables;

        public ProcessHelper(Process process, Action<string> onDataReceived = null, Action<string> onErrorReceived = null)
        {
            try
            {
                _environmentVariables = new(process.StartInfo.Environment);
            }
            catch
            {
                // ...
            }

            Task = Task.WhenAll(_outputTask.Task, _errorTask.Task, _processExit.Task);

            Task.Factory.StartNew(
                () =>
                {
                    process.WaitForExit();
                    _processExit.TrySetResult(true);
                },
                TaskCreationOptions.LongRunning);

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                {
                    _outputTask.TrySetResult(true);
                }
                else
                {
                    _outputBuffer.AppendLine(args.Data);
                    try
                    {
                        onDataReceived?.Invoke(args.Data);
                    }
                    catch (Exception)
                    {
                    }
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                {
                    _errorTask.TrySetResult(true);
                }
                else
                {
                    _errorBuffer.AppendLine(args.Data);
                    try
                    {
                        (onErrorReceived ?? onDataReceived)?.Invoke(args.Data);
                    }
                    catch (Exception)
                    {
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Process = process;
        }

        public Process Process { get; }

        public string StandardOutput => _outputBuffer.ToString();

        public string ErrorOutput => _errorBuffer.ToString();

        public ReadOnlyDictionary<string, string> EnvironmentVariables => _environmentVariables;

        public Task Task { get; }

        public bool Drain(int timeout = Timeout.Infinite)
        {
            // Wait for all output and error to be drained
            if (timeout != Timeout.Infinite)
            {
                // Split timeout between output, error, and process exit
                timeout /= 3;
            }

            return _outputTask.Task.Wait(timeout)
                && _errorTask.Task.Wait(timeout)
                && Process.WaitForExit(timeout);
        }

        public virtual void Dispose()
        {
            if (!Process.HasExited)
            {
                try
                {
                    Process.Kill();
                }
                catch
                {
                    // Ignore exceptions when killing the process, as it may have already exited
                }
            }

            // Wait for output/error draining to complete
            Task?.Wait();
            Process?.Dispose();
        }
    }
}
