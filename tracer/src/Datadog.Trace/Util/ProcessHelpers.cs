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
        /// Gets (and clears) the "do not trace" state for the current thread's call to <see cref="Process.Start()"/>
        /// </summary>
        /// <returns>True if the <see cref="Process.Start()"/> call should be traced, False if "do not trace" is set</returns>
        public static bool ShouldTraceProcessStart() => !_doNotTrace;

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

            if (input is not null)
            {
                processInfo.StandardInput.Write(input);
                processInfo.StandardInput.Flush();
                processInfo.StandardInput.Close();
            }

            var outputStringBuilder = new StringBuilder();
            var errorStringBuilder = new StringBuilder();
            while (!processInfo.HasExited)
            {
                if (!processStartInfo.UseShellExecute)
                {
                    outputStringBuilder.Append(processInfo.StandardOutput.ReadToEnd());
                    errorStringBuilder.Append(processInfo.StandardError.ReadToEnd());
                }

                Thread.Sleep(15);
            }

            if (!processStartInfo.UseShellExecute)
            {
                outputStringBuilder.Append(processInfo.StandardOutput.ReadToEnd());
                errorStringBuilder.Append(processInfo.StandardError.ReadToEnd());
            }

            Log.Debug<int>("Process finished with exit code: {Value}.", processInfo.ExitCode);
            return new CommandOutput(outputStringBuilder.ToString(), errorStringBuilder.ToString(), processInfo.ExitCode);
        }

        /// <summary>
        /// Run a command and get the standard output content as a string
        /// </summary>
        /// <param name="command">Command to run</param>
        /// <param name="input">Standard input content</param>
        /// <returns>Task with the output of the command</returns>
        public static async Task<CommandOutput?> RunCommandAsync(Command command, string? input = null)
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
                    var cmdResponse = await RunCommandAsync(new Command("whereis", $"-b {processStartInfo.FileName}")).ConfigureAwait(false);
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
                    var cmdResponse = await RunCommandAsync(new Command("where", processStartInfo.FileName)).ConfigureAwait(false);
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
            if (input is not null)
            {
                await processInfo.StandardInput.WriteAsync(input).ConfigureAwait(false);
                await processInfo.StandardInput.FlushAsync().ConfigureAwait(false);
                processInfo.StandardInput.Close();
            }

            var outputStringBuilder = new StringBuilder();
            var errorStringBuilder = new StringBuilder();
            while (!processInfo.HasExited)
            {
                if (!processStartInfo.UseShellExecute)
                {
                    outputStringBuilder.Append(await processInfo.StandardOutput.ReadToEndAsync().ConfigureAwait(false));
                    errorStringBuilder.Append(await processInfo.StandardError.ReadToEndAsync().ConfigureAwait(false));
                }

                await Task.Delay(15).ConfigureAwait(false);
            }

            if (!processStartInfo.UseShellExecute)
            {
                outputStringBuilder.Append(await processInfo.StandardOutput.ReadToEndAsync().ConfigureAwait(false));
                errorStringBuilder.Append(await processInfo.StandardError.ReadToEndAsync().ConfigureAwait(false));
            }

            Log.Debug<int>("Process finished with exit code: {Value}.", processInfo.ExitCode);
            return new CommandOutput(outputStringBuilder.ToString(), errorStringBuilder.ToString(), processInfo.ExitCode);
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
            if (parts.Length > 1 && parts[0].EndsWith(":"))
            {
                return parts.Skip(1);
            }

            return []; // Return empty if no valid path is found
        }

        /// <summary>
        /// Internal for testing to make it easier to call using reflection from a sample app
        /// </summary>
        internal static void TestingOnly_RunCommand(string cmd, string? args)
        {
            RunCommand(new Command(cmd, args));
        }

        private static ProcessStartInfo GetProcessStartInfo(Command command)
        {
            var processStartInfo = command.Arguments is null ?
                                       new ProcessStartInfo(command.Cmd) :
                                       new ProcessStartInfo(command.Cmd, command.Arguments);
            processStartInfo.CreateNoWindow = true;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.Verb = command.Verb;
            if (command.Verb is null)
            {
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.StandardOutputEncoding = command.OutputEncoding ?? processStartInfo.StandardOutputEncoding;
                processStartInfo.StandardErrorEncoding = command.ErrorEncoding ?? processStartInfo.StandardErrorEncoding;
            }
            else
            {
                processStartInfo.UseShellExecute = true;
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

            public Command(string cmd, string? arguments = null, string? workingDirectory = null, string? verb = null, Encoding? outputEncoding = null, Encoding? errorEncoding = null, Encoding? inputEncoding = null, bool doNotTrace = true, bool useWhereIsIfFileNotFound = false)
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
            }
        }

        public class CommandOutput
        {
            public CommandOutput(string output, string error, int exitCode)
            {
                Output = output;
                Error = error;
                ExitCode = exitCode;
            }

            public string Output { get; }

            public string Error { get; }

            public int ExitCode { get; }
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
    }
}
