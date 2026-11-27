// <copyright file="ProcessHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Util
{
    internal static class ProcessHelpers
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessHelpers));

        [ThreadStatic]
        private static bool _doNotTrace;

        /// <summary>
        /// Wrapper around <see cref="Process.GetCurrentProcess"/> and <see cref="Process.ProcessName"/>
        ///
        /// On .NET Framework the <see cref="Process"/> class is guarded by a
        /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// This exception is thrown when the caller method is being JIT compiled, NOT
        /// when Process.GetCurrentProcess is called, so this wrapper method allows
        /// us to catch the exception.
        /// </summary>
        /// <returns>Returns the name of the current process</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentProcessName()
        {
            return CurrentProcess.ProcessName;
        }

        /// <summary>
        /// Wrapper around <see cref="Process.GetCurrentProcess"/> and its property accesses
        ///
        /// On .NET Framework the <see cref="Process"/> class is guarded by a
        /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// This exception is thrown when the caller method is being JIT compiled, NOT
        /// when Process.GetCurrentProcess is called, so this wrapper method allows
        /// us to catch the exception.
        /// </summary>
        /// <param name="processName">The name of the current process</param>
        /// <param name="machineName">The machine name of the current process</param>
        /// <param name="processId">The ID of the current process</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetCurrentProcessInformation(out string processName, out string machineName, out int processId)
        {
            processName = CurrentProcess.ProcessName;
            machineName = CurrentProcess.MachineName;
            processId = CurrentProcess.Pid;
        }

        /// <summary>
        /// Gets the "do not trace" state for the current thread's call to <see cref="Process.Start()"/>
        /// </summary>
        /// <returns>True if the <see cref="Process.Start()"/> call should be traced, False if "do not trace" is set</returns>
        public static bool ShouldTraceProcessStart() => !_doNotTrace;

        /// <summary>
        /// Used to override the "do not trace" state for the current thread's call to <see cref="Process.Start()"/>.
        /// Prefer using <see cref="StartWithDoNotTrace"/> - this should only be used to work around version conflict scenarios
        /// </summary>
        public static void ForceDoNotTrace(bool doNotTrace) => _doNotTrace = doNotTrace;

        /// <summary>
        /// Run a command and get the standard output content as a string
        /// </summary>
        /// <param name="command">Command to run</param>
        /// <param name="input">Standard input content</param>
        /// <returns>The output of the command</returns>
        public static CommandOutput? RunCommand(Command command, string? input = null)
        {
            Log.Debug("Running command: {Command} {Args}", command.Cmd, command.Arguments);
            var processStartInfo = GetProcessStartInfo(command);
            if (input is not null)
            {
                processStartInfo.RedirectStandardInput = true;
#if NETCOREAPP
                processStartInfo.StandardInputEncoding = command.InputEncoding ?? processStartInfo.StandardInputEncoding;
#endif
            }

            Process? processInfo = null;
            try
            {
                processInfo = StartWithDoNotTrace(processStartInfo, command.DoNotTrace);
            }
            catch (System.ComponentModel.Win32Exception) when (command.UseWhereIsIfFileNotFound)
            {
                if (FrameworkDescription.Instance.OSDescription == OSPlatformName.Linux &&
                    !string.Equals(command.Cmd, "whereis", StringComparison.OrdinalIgnoreCase))
                {
                    var cmdResponse = RunCommand(new Command("whereis", $"-b {processStartInfo.FileName}"));
                    if (cmdResponse?.ExitCode == 0 &&
                        cmdResponse.Output.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } outputLines &&
                        outputLines[0] is { Length: > 0 } temporalOutput)
                    {
                        foreach (var path in ParseWhereisOutput(temporalOutput))
                        {
                            if (File.Exists(path))
                            {
                                processStartInfo.FileName = path;
                                processInfo = StartWithDoNotTrace(processStartInfo, command.DoNotTrace);
                                break;
                            }
                        }
                    }
                }
                else if (!string.Equals(command.Cmd, "where", StringComparison.OrdinalIgnoreCase))
                {
                    var cmdResponse = RunCommand(new Command("where", processStartInfo.FileName));
                    if (cmdResponse?.ExitCode == 0 &&
                        cmdResponse.Output.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } outputLines &&
                        outputLines[0] is { Length: > 0 } processPath &&
                        File.Exists(processPath))
                    {
                        processStartInfo.FileName = processPath;
                        processInfo = StartWithDoNotTrace(processStartInfo, command.DoNotTrace);
                    }
                }
                else
                {
                    throw;
                }
            }

            if (processInfo is null)
            {
                return null;
            }

            using var disposableProcessInfo = processInfo;

            // The output is read on this thread until the process closes it, so a timer enforces the timeout by killing the
            // process, which ends the reads. Reading asynchronously isn't used: it caused deadlocks on Alpine arm64 (#6545) and
            // broke MSTest AppDomain unloading on .NET Framework.
            using var timeoutKiller = command.Timeout is { } timeout ? new TimeoutKiller(processInfo, timeout) : null;

            if (input is not null)
            {
                processInfo.StandardInput.Write(input);
                processInfo.StandardInput.Flush();
                processInfo.StandardInput.Close();
            }

            var output = string.Empty;
            var error = string.Empty;
            if (!processStartInfo.UseShellExecute)
            {
                output = processInfo.StandardOutput.ReadToEnd();
                error = processInfo.StandardError.ReadToEnd();
            }

            processInfo.WaitForExit();
            if (timeoutKiller?.TimedOut == true)
            {
                return new CommandOutput(output, error, exitCode: -1, timedOut: true);
            }

            Log.Debug<int>("Process finished with exit code: {Value}.", processInfo.ExitCode);
            return new CommandOutput(output, error, processInfo.ExitCode, timedOut: false);
        }

        internal static IEnumerable<string> ParseWhereisOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return [];
            }

            // Split the string by spaces to separate parts
            var parts = output.Split(' ');

            // Check if the first part ends with a colon (e.g., "dotnet:")
            if (parts.Length > 1 && parts[0].EndsWith(':'))
            {
                return parts.Skip(1);
            }

            return []; // Return empty if no valid path is found
        }

        /// <summary>
        /// Internal for testing to make it easier to call using reflection from a sample app
        /// </summary>
        [TestingOnly]
        internal static void TestingOnly_RunCommand(string cmd, string? args)
        {
            RunCommand(new Command(cmd, args));
        }

        private static ProcessStartInfo GetProcessStartInfo(Command command)
        {
            // A verb (e.g. "runas") requires the shell; otherwise we redirect the output, which requires UseShellExecute = false
            var useShellExecute = command.Verb is not null;
            var processStartInfo = new ProcessStartInfo(command.Cmd, command.Arguments ?? string.Empty) { UseShellExecute = useShellExecute };
            processStartInfo.CreateNoWindow = true;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.Verb = command.Verb;
            if (!useShellExecute)
            {
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.StandardOutputEncoding = command.OutputEncoding ?? processStartInfo.StandardOutputEncoding;
                processStartInfo.StandardErrorEncoding = command.ErrorEncoding ?? processStartInfo.StandardErrorEncoding;
            }

            if (command.WorkingDirectory is not null)
            {
                processStartInfo.WorkingDirectory = command.WorkingDirectory;
            }

            return processStartInfo;
        }

        private static Process? StartWithDoNotTrace(ProcessStartInfo startInfo, bool doNotTrace)
        {
            try
            {
                _doNotTrace = doNotTrace;
                return Process.Start(startInfo);
            }
            finally
            {
                _doNotTrace = false;
            }
        }

        public readonly struct Command
        {
            public readonly string Cmd;
            public readonly string? Arguments;
            public readonly string? WorkingDirectory;
            public readonly string? Verb;
            public readonly Encoding? OutputEncoding;
            public readonly Encoding? ErrorEncoding;
            public readonly Encoding? InputEncoding;
            public readonly bool DoNotTrace;
            public readonly bool UseWhereIsIfFileNotFound;
            public readonly TimeSpan? Timeout;

            public Command(string cmd, string? arguments = null, string? workingDirectory = null, string? verb = null, Encoding? outputEncoding = null, Encoding? errorEncoding = null, Encoding? inputEncoding = null, bool doNotTrace = true, bool useWhereIsIfFileNotFound = false, TimeSpan? timeout = null)
            {
                Cmd = cmd;
                Arguments = arguments;
                WorkingDirectory = workingDirectory;
                Verb = verb;
                OutputEncoding = outputEncoding;
                ErrorEncoding = errorEncoding;
                InputEncoding = inputEncoding;
                DoNotTrace = doNotTrace;
                UseWhereIsIfFileNotFound = useWhereIsIfFileNotFound;
                Timeout = timeout;
            }
        }

        public sealed class CommandOutput
        {
            public CommandOutput(string output, string error, int exitCode, bool timedOut)
            {
                Output = output;
                Error = error;
                ExitCode = exitCode;
                TimedOut = timedOut;
            }

            [JsonProperty("output")]
            public string Output { get; }

            [JsonProperty("error")]
            public string Error { get; }

            [JsonProperty("exitCode")]
            public int ExitCode { get; }

            [JsonProperty("timedOut")]
            public bool TimedOut { get; }

            [JsonIgnore]
            public bool Cached { get; internal set; }
        }

        private static class CurrentProcess
        {
            internal static readonly string ProcessName;
            internal static readonly string MachineName;
            internal static readonly int Pid;

            static CurrentProcess()
            {
                using var process = Process.GetCurrentProcess();

                // Cache the information that won't change
                ProcessName = process.ProcessName;
                MachineName = process.MachineName;
                Pid = process.Id;
            }
        }

        /// <summary>
        /// Kills a process that is still running when its timeout elapses.
        /// </summary>
        private sealed class TimeoutKiller : IDisposable
        {
            private readonly Process _process;
            private readonly TimeSpan _timeout;
            private readonly Timer _timer;
            private int _timedOut;

            public TimeoutKiller(Process process, TimeSpan timeout)
            {
                _process = process;
                _timeout = timeout;
                _timer = new Timer(static state => ((TimeoutKiller)state!).Kill(), this, timeout, Timeout.InfiniteTimeSpan);
            }

            public bool TimedOut => Volatile.Read(ref _timedOut) == 1;

            public void Dispose() => _timer.Dispose();

            private void Kill()
            {
                try
                {
                    if (_process.HasExited)
                    {
                        return;
                    }

                    // Set before killing: the reads end as soon as the process is gone, possibly before Kill returns.
                    Volatile.Write(ref _timedOut, 1);
                    Log.Warning<TimeSpan>("Process timed out after {Timeout}. Killing process.", _timeout);
#if NETCOREAPP3_0_OR_GREATER
                    // Child processes that inherited the output pipes would keep the reads waiting.
                    _process.Kill(entireProcessTree: true);
#else
                    _process.Kill();
#endif
                }
                catch (Exception ex)
                {
                    // The process exited, or RunCommand returned and disposed it, in the meantime.
                    Log.Debug(ex, "Error killing the process after its timeout.");
                }
            }
        }
    }
}
