// <copyright file="AgentProcessManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// This class is used to manage agent processes in contexts where the user can not, such as Azure App Services.
    /// </summary>
    internal static class AgentProcessManager
    {
        internal const int KeepAliveInterval = 10_000;
        internal const int ExceptionRetryInterval = 2_000;
        internal const int MaxRetryInterval = 60_000;
        internal const int MaxFailedHealthChecks = 3;

        private const int ErrorSemTimeout = 121;
        private const int ErrorPipeBusy = 231;
        private const int TraceAgentConnectTimeout = 500;
        private const int TraceAgentResponseTimeout = 2_000;
        private const int StopTimeout = 1_000;

        internal static readonly ProcessMetadata TraceAgentMetadata = new ProcessMetadata
        {
            Name = "datadog-trace-agent",
            IsTraceAgent = true,
            PipeName = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.TracesPipeName),
            ProcessPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.TraceAgentPath),
            ProcessArguments = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.TraceAgentArgs),
        };

        internal static readonly ProcessMetadata DogStatsDMetadata = new ProcessMetadata
        {
            Name = "dogstatsd",
            PipeName = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.MetricsPipeName),
            ProcessPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.DogStatsDPath),
            ProcessArguments = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.DogStatsDArgs),
        };

        private static readonly List<ProcessMetadata> Processes = new List<ProcessMetadata>(2);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AgentProcessManager));

        private static readonly byte[] TraceAgentInfoRequest = Encoding.ASCII.GetBytes("GET /info HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n");

        internal enum ProcessState
        {
            NeverChecked,
            ReadyToStart,
            Faulted,
            Healthy,

            /// <summary>
            /// The process started by this keep-alive is still running but the health check fails.
            /// </summary>
            Unhealthy
        }

        internal enum KeepAliveAction
        {
            None,
            Start,
            Restart
        }

        /// <summary>
        /// Invoked by the loader
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (DomainMetadata.Instance.ShouldAvoidAppDomain())
                {
                    Log.Information("Skipping process manager initialization for AppDomain: {AppDomain}", DomainMetadata.Instance.AppDomainName);
                    return;
                }

                // This is run from the loader, so no point recording telemetry as we're not going to send it anyway
                var azureAppServiceSettings = new ImmutableAzureAppServiceSettings(GlobalConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
                if (azureAppServiceSettings.IsUnsafeToTrace)
                {
                    Log.ErrorSkipTelemetry("The Azure Site Extension doesn't have the required parameters to work. The API_KEY is likely missing. The trace_agent and dogstatsd process will not be started. Check your app configuration and restart the app service to try again.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(TraceAgentMetadata.ProcessPath))
                {
                    Log.Warning("Requested to start the Trace Agent but the process path hasn't been supplied in environment.");
                }
                else if (!Directory.Exists(TraceAgentMetadata.DirectoryPath))
                {
                    Log.Warning("Directory for trace agent does not exist: {Directory}. The process won't be started.", TraceAgentMetadata.DirectoryPath);
                }
                else
                {
                    Processes.Add(TraceAgentMetadata);
                }

                if (string.IsNullOrWhiteSpace(DogStatsDMetadata.ProcessPath))
                {
                    Log.Warning("Requested to start dogstatsd but the process path hasn't been supplied in environment.");
                }
                else if (!Directory.Exists(DogStatsDMetadata.DirectoryPath))
                {
                    Log.Warning("Directory for dogstatsd does not exist: {Directory}. The process won't be started.", DogStatsDMetadata.DirectoryPath);
                }
                else
                {
                    Processes.Add(DogStatsDMetadata);
                }

                if (Processes.Count > 0)
                {
                    Log.Debug("Starting {Count} child processes from process {ProcessName}, AppDomain {AppDomain}.", Processes.Count, DomainMetadata.Instance.ProcessName, DomainMetadata.Instance.AppDomainName);
                    StartProcesses(azureAppServiceSettings);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when attempting to initialize process manager.");
            }
        }

        private static void StartProcesses(ImmutableAzureAppServiceSettings azureAppServiceSettings)
        {
            if (azureAppServiceSettings.DebugModeEnabled)
            {
                if (EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.Agent.LogLevel) == null)
                {
                    // This ensures that a single setting from applicationConfig can enable debug logs for every aspect of the extension
                    EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Agent.LogLevel, "debug");
                }
            }

            foreach (var metadata in Processes)
            {
                if (string.IsNullOrWhiteSpace(metadata.ProcessPath))
                {
                    Log.Debug("There is no path configured for {ProcessName}.", metadata.Name);
                }
                else if (!File.Exists(metadata.ProcessPath))
                {
                    Log.Warning("Request path for {Name} does not exist: {Path}. The process won't be started.", metadata.Name, metadata.ProcessPath);
                }
                else
                {
                    if (!metadata.IsBeingManaged)
                    {
                        metadata.KeepAliveTask = StartProcessWithKeepAlive(metadata);
                    }
                }
            }
        }

        private static Task StartProcessWithKeepAlive(ProcessMetadata metadata)
        {
            var path = metadata.ProcessPath;
            Log.Debug("Starting keep alive for {Process}.", path);

            return Task.Run(
                async () =>
                {
                    try
                    {
                        // Indicate that there is a worker responsible for doing this
                        // This acts as a lock
                        metadata.IsBeingManaged = true;

                        while (true)
                        {
                            try
                            {
                                if (metadata.ProcessState == ProcessState.NeverChecked)
                                {
                                    // This means we have never tried from this domain

                                    if (await metadata.ProcessIsHealthyAsync().ConfigureAwait(false))
                                    {
                                        // This means one of two things:
                                        // - Another domain has already started this process
                                        // - The previous instance is in the process of shutting down
                                        // It is possible that if a pipe is bound it may yet be cleaned up from a previous shutdown

                                        // Assume healthy to start, but look for problems
                                        metadata.ProcessState = ProcessState.Healthy;

                                        // Check on a delay to be sure we keep the process available after a shutdown
                                        var attempts = 7;
                                        var delay = 50d;

                                        while (--attempts > 0)
                                        {
                                            await Task.Delay((int)delay).ConfigureAwait(false);

                                            if (await metadata.ProcessIsHealthyAsync().ConfigureAwait(false))
                                            {
                                                // Should result in a max delay of ~3.28 seconds before giving up
                                                delay = delay * 1.75d;
                                                continue;
                                            }

                                            // The previous instance is gone, time to start the process
                                            Log.Information("Recovering from previous {Process} shutdown. Ready for start.", path);
                                            metadata.ProcessState = ProcessState.ReadyToStart;
                                            break;
                                        }

                                        if (metadata.ProcessState == ProcessState.Healthy)
                                        {
                                            Log.Information("{Process} is already running and was not started by this process. Using it.", path);
                                        }
                                    }
                                    else
                                    {
                                        // If no instance exists, just get ready to start
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                    }
                                }
                                else
                                {
                                    // This means we have tried to start from this domain before
                                    var previousState = metadata.ProcessState;
                                    LogIfOwnProcessExited(metadata);
                                    var isHealthy = await metadata.ProcessIsHealthyAsync().ConfigureAwait(false);
                                    metadata.FailedHealthChecks = isHealthy ? 0 : metadata.FailedHealthChecks + 1;
                                    var action = GetKeepAliveAction(isHealthy, metadata.OwnProcessIsRunning(), metadata.FailedHealthChecks);
                                    if (isHealthy)
                                    {
                                        metadata.SequentialFailures = 0;
                                        metadata.ProcessState = ProcessState.Healthy;
                                        if (previousState == ProcessState.Unhealthy)
                                        {
                                            Log.Information("{Process} with pid {ChildPid} is now healthy.", path, GetProcessId(metadata.Process));
                                        }
                                    }
                                    else if (action == KeepAliveAction.Start)
                                    {
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                        if (previousState == ProcessState.Healthy)
                                        {
                                            Log.Information("{Process} is no longer available. Starting a new one.", path);
                                        }
                                    }
                                    else if (action == KeepAliveAction.Restart)
                                    {
                                        // Only reached for the process we started: we never stop an agent we did not start.
                                        // Log once; later attempts only happen if stopping it failed.
                                        var firstAttempt = metadata.FailedHealthChecks == MaxFailedHealthChecks;
                                        if (firstAttempt)
                                        {
                                            Log.Warning<string, string, int>("{Process} with pid {ChildPid} started by this process failed {Count} health checks in a row. Restarting it.", path, GetProcessId(metadata.Process), metadata.FailedHealthChecks);
                                        }

                                        metadata.ProcessState = TryStopOwnProcess(metadata, firstAttempt) ? ProcessState.ReadyToStart : ProcessState.Unhealthy;
                                    }
                                    else
                                    {
                                        // The process we started is still running: never start a second copy
                                        metadata.ProcessState = ProcessState.Unhealthy;
                                        if (previousState != ProcessState.Unhealthy)
                                        {
                                            Log.Warning("{Process} with pid {ChildPid} started by this process is running but failed its health check.", path, GetProcessId(metadata.Process));
                                        }
                                    }
                                }

                                if (metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    Log.Debug("Attempting to start {Process}.", path);
                                    metadata.FailedHealthChecks = 0;

                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = path, UseShellExecute = false // Force consistency in behavior between Framework and Core
                                    };

                                    if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                    {
                                        startInfo.Arguments = metadata.ProcessArguments;
                                    }

                                    metadata.Process = Process.Start(startInfo);
                                    var childPid = GetProcessId(metadata.Process);
                                    Log.Information("Started {Process} with pid {ChildPid}.", path, childPid);

                                    const int startupTimeout = 2000;
                                    var startupStopwatch = Stopwatch.StartNew();

                                    while (startupStopwatch.ElapsedMilliseconds < startupTimeout)
                                    {
                                        // Loop and watch for evidence that the process is up and running
                                        if (await metadata.ProcessIsHealthyAsync().ConfigureAwait(false))
                                        {
                                            metadata.SequentialFailures = 0;
                                            metadata.ProcessState = ProcessState.Healthy;
                                            if (metadata.Process == null || metadata.Process.HasExited)
                                            {
                                                // The health check passed because of another instance, not the one we started
                                                Log.Information(
                                                    "{Process} with pid {ChildPid} exited with code {ExitCode}, but another instance is already running. Using it.",
                                                    path,
                                                    childPid,
                                                    GetExitCode(metadata.Process));
                                            }
                                            else
                                            {
                                                Log.Information("Successfully started {Process} with pid {ChildPid}.", path, childPid);
                                            }

                                            break;
                                        }

                                        if (metadata.Process == null || metadata.Process.HasExited)
                                        {
                                            Log.Error("Failed to start {Process}: pid {ChildPid} exited with code {ExitCode}.", path, childPid, GetExitCode(metadata.Process));
                                            metadata.ProcessState = ProcessState.Faulted;
                                            break;
                                        }

                                        await Task.Delay(100).ConfigureAwait(false);
                                    }

                                    if (metadata.ProcessState == ProcessState.ReadyToStart)
                                    {
                                        Log.Warning<string, string, int>("{Process} with pid {ChildPid} is running but did not pass its health check within {Timeout} ms.", path, childPid, startupTimeout);
                                        metadata.ProcessState = ProcessState.Unhealthy;
                                    }
                                }
                            }
                            catch (ThreadAbortException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                metadata.ProcessState = ProcessState.Faulted;
                                Log.Error(ex, "Failed to start {Process}.", path);
                            }
                            finally
                            {
                                if (metadata.ProcessState == ProcessState.Faulted)
                                {
                                    metadata.SequentialFailures++;
                                    // Quicker retry in these cases, backing off while failures repeat
                                    await Task.Delay(GetRetryDelay(metadata.SequentialFailures)).ConfigureAwait(false);
                                }
                                else
                                {
                                    // Delay for a reasonable amount of time before we check to see if the process is alive again.
                                    await Task.Delay(KeepAliveInterval).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error occurred in keep-alive for {Process}.", path);
                    }
                    finally
                    {
                        Log.Warning("Keep alive is dropping for {Process}.", path);
                        metadata.IsBeingManaged = false;
                    }
                });
        }

        /// <summary>
        /// Decides what the keep-alive does after a health check. A new process is started only when
        /// the agent is unhealthy and the process this keep-alive started is no longer running. The process
        /// this keep-alive started is restarted after <see cref="MaxFailedHealthChecks"/> failed checks in a row.
        /// </summary>
        internal static KeepAliveAction GetKeepAliveAction(bool isHealthy, bool ownProcessIsRunning, int failedHealthChecks)
        {
            if (isHealthy)
            {
                return KeepAliveAction.None;
            }

            if (!ownProcessIsRunning)
            {
                return KeepAliveAction.Start;
            }

            // Only the process we started is ever restarted
            return failedHealthChecks >= MaxFailedHealthChecks ? KeepAliveAction.Restart : KeepAliveAction.None;
        }

        /// <summary>
        /// Kills the process this keep-alive started. Returns false if it is still running afterwards,
        /// in which case it stays tracked so that a second copy is never started next to it.
        /// </summary>
        private static bool TryStopOwnProcess(ProcessMetadata metadata, bool logErrorAsWarning)
        {
            var process = metadata.Process;
            if (process == null)
            {
                return true;
            }

            try
            {
                process.Kill();
                process.WaitForExit(StopTimeout);
            }
            catch (Exception ex)
            {
                if (logErrorAsWarning)
                {
                    Log.Warning<string, string>(ex, "Error when stopping {Process} with pid {ChildPid}.", metadata.ProcessPath, GetProcessId(process));
                }
                else
                {
                    Log.Debug<string, string>(ex, "Error when stopping {Process} with pid {ChildPid}.", metadata.ProcessPath, GetProcessId(process));
                }
            }

            if (metadata.OwnProcessIsRunning())
            {
                return false;
            }

            metadata.Process = null;
            process.Dispose();
            return true;
        }

        /// <summary>
        /// Gets the delay before retrying after a failed start: <see cref="ExceptionRetryInterval"/> after the
        /// first failure, doubling with each further failure up to <see cref="MaxRetryInterval"/>. Retries never stop.
        /// </summary>
        internal static int GetRetryDelay(int sequentialFailures)
        {
            var delay = ExceptionRetryInterval;
            for (var i = 1; i < sequentialFailures && delay < MaxRetryInterval; i++)
            {
                delay *= 2;
            }

            return Math.Min(delay, MaxRetryInterval);
        }

        private static void LogIfOwnProcessExited(ProcessMetadata metadata)
        {
            var process = metadata.Process;
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    return;
                }

                Log.Information(
                    "{Process} with pid {ChildPid} started by this process exited with code {ExitCode}.",
                    metadata.ProcessPath,
                    GetProcessId(process),
                    GetExitCode(process));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error when checking whether {Process} exited.", metadata.ProcessPath);
                return;
            }

            // Only report each exit once
            metadata.Process = null;
            process.Dispose();
        }

        /// <summary>
        /// Checks whether a server instance of the named pipe exists, without connecting to it.
        /// A pipe whose instances are all busy still exists.
        /// </summary>
        private static bool NamedPipeExists(string namedPipe)
        {
            if (!FrameworkDescription.Instance.IsWindows())
            {
                return File.Exists(namedPipe);
            }

            // 1 ms keeps the check cheap: 0 would wait for the server's default timeout
            if (WaitNamedPipe(namedPipe, 1))
            {
                return true;
            }

            // ERROR_FILE_NOT_FOUND (and anything else unexpected) means no usable pipe
            var error = Marshal.GetLastWin32Error();
            return error == ErrorSemTimeout || error == ErrorPipeBusy;
        }

        [DllImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WaitNamedPipe(string name, uint timeout);

        private static string GetProcessId(Process process)
        {
            try
            {
                return process?.Id.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static string GetExitCode(Process process)
        {
            try
            {
                return process?.ExitCode.ToString(CultureInfo.InvariantCulture) ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        internal sealed class ProcessMetadata
        {
            private string _processPath;

            public string PipeName { get; set; }

            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is being managed by active keep alive tasks.
            /// </summary>
            public bool IsBeingManaged { get; set; }

            public int SequentialFailures { get; set; }

            public int FailedHealthChecks { get; set; }

            public ProcessState ProcessState { get; set; } = ProcessState.NeverChecked;

            public string ProcessPath
            {
                get => _processPath;
                set
                {
                    _processPath = value;
                    DirectoryPath = Path.GetDirectoryName(_processPath);
                }
            }

            public string DirectoryPath { get; private set; }

            public string ProcessArguments { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the health check sends a request to the agent over its pipe.
            /// </summary>
            public bool IsTraceAgent { get; set; }

            public bool OwnProcessIsRunning()
            {
                try
                {
                    return Process != null && !Process.HasExited;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error when checking whether {Process} is running.", ProcessPath);
                    return false;
                }
            }

            /// <summary>
            /// The agent is healthy only while its named pipe exists, whoever started it. A process that
            /// matches by name but was not started by us does not count; the process we started is
            /// tracked through <see cref="OwnProcessIsRunning"/> instead.
            /// </summary>
            public async Task<bool> ProcessIsHealthyAsync()
            {
                if (!NamedPipeIsBound())
                {
                    return false;
                }

                // DogStatsD never replies, so its pipe being present is all we can check
                return !IsTraceAgent || await TraceAgentRespondsAsync().ConfigureAwait(false);
            }

            private bool NamedPipeIsBound()
            {
                var namedPipe = $"\\\\.\\pipe\\{PipeName}";
                var namedPipeIsBound = NamedPipeExists(namedPipe);
                if (!namedPipeIsBound)
                {
                    Log.Debug("NamedPipe  [{NamedPipe}] is not present.", namedPipe);
                }

                return namedPipeIsBound;
            }

            /// <summary>
            /// Sends GET /info over the trace agent's pipe and checks that an HTTP response comes back.
            /// The connect and the response are both bounded, so a hung agent fails the check.
            /// </summary>
            private async Task<bool> TraceAgentRespondsAsync()
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
                using var timeoutCancellation = new CancellationTokenSource();
                var exchange = SendInfoRequestAsync(pipe);
                var completed = await Task.WhenAny(exchange, Task.Delay(TraceAgentResponseTimeout, timeoutCancellation.Token)).ConfigureAwait(false);
                if (completed == exchange)
                {
                    timeoutCancellation.Cancel();
                    return await exchange.ConfigureAwait(false);
                }

                // Timed out: disposing the pipe aborts the pending read
                return false;
            }

            private async Task<bool> SendInfoRequestAsync(NamedPipeClientStream pipe)
            {
                try
                {
                    await pipe.ConnectAsync(TraceAgentConnectTimeout).ConfigureAwait(false);
                    await pipe.WriteAsync(TraceAgentInfoRequest, 0, TraceAgentInfoRequest.Length).ConfigureAwait(false);

                    // Any status line means the agent is serving requests
                    var statusLineStart = new byte[5];
                    var read = 0;
                    while (read < statusLineStart.Length)
                    {
                        var count = await pipe.ReadAsync(statusLineStart, read, statusLineStart.Length - read).ConfigureAwait(false);
                        if (count == 0)
                        {
                            return false;
                        }

                        read += count;
                    }

                    return Encoding.ASCII.GetString(statusLineStart) == "HTTP/";
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Trace agent did not respond on named pipe {PipeName}.", PipeName);
                    return false;
                }
            }
        }
    }
}
