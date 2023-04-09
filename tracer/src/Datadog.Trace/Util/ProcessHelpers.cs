// <copyright file="ProcessHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    internal static class ProcessHelpers
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessHelpers));

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
            using var currentProcess = Process.GetCurrentProcess();
            return currentProcess.ProcessName;
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
            using var currentProcess = Process.GetCurrentProcess();
            processName = currentProcess.ProcessName;
            machineName = currentProcess.MachineName;
            processId = currentProcess.Id;
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
        /// <param name="userProcessorTime">CPU time in user mode</param>
        /// <param name="systemCpuTime">CPU time in kernel mode</param>
        /// <param name="threadCount">Number of threads</param>
        /// <param name="privateMemorySize">Committed memory size</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GetCurrentProcessRuntimeMetrics(out TimeSpan userProcessorTime, out TimeSpan systemCpuTime, out int threadCount, out long privateMemorySize)
        {
            using var process = Process.GetCurrentProcess();
            userProcessorTime = process.UserProcessorTime;
            systemCpuTime = process.PrivilegedProcessorTime;
            threadCount = process.Threads.Count;
            privateMemorySize = process.PrivateMemorySize64;
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
            }

            using var processInfo = Process.Start(processStartInfo);
            if (processInfo is null)
            {
                return null;
            }

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
                outputStringBuilder.Append(await processInfo.StandardOutput.ReadToEndAsync().ConfigureAwait(false));
                errorStringBuilder.Append(await processInfo.StandardError.ReadToEndAsync().ConfigureAwait(false));
                await Task.Delay(15).ConfigureAwait(false);
            }

            outputStringBuilder.Append(await processInfo.StandardOutput.ReadToEndAsync().ConfigureAwait(false));
            errorStringBuilder.Append(await processInfo.StandardError.ReadToEndAsync().ConfigureAwait(false));

            Log.Debug<int>("Process finished with exit code: {Value}.", processInfo.ExitCode);
            return new CommandOutput(outputStringBuilder.ToString(), errorStringBuilder.ToString(), processInfo.ExitCode);
        }

        private static ProcessStartInfo GetProcessStartInfo(Command command)
        {
            var processStartInfo = command.Arguments is null ?
                                       new ProcessStartInfo(command.Cmd) :
                                       new ProcessStartInfo(command.Cmd, command.Arguments);
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;

            if (command.WorkingDirectory is not null)
            {
                processStartInfo.WorkingDirectory = command.WorkingDirectory;
            }

            return processStartInfo;
        }

        public readonly struct Command
        {
            public readonly string Cmd;
            public readonly string? Arguments;
            public readonly string? WorkingDirectory;

            public Command(string cmd, string? arguments = null, string? workingDirectory = null)
            {
                Cmd = cmd;
                Arguments = arguments;
                WorkingDirectory = workingDirectory;
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
    }
}
