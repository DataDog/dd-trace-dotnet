// <copyright file="AgentProcessManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Datadog.Trace.SourceGenerators;
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
        internal const int StableHealthyPeriod = 60_000;
        internal const int MaxFailedHealthChecks = 3;
        internal const int TraceAgentResponseTimeout = 2_000;

        private const int ErrorSemTimeout = 121;
        private const int ErrorPipeBusy = 231;
        private const int TraceAgentConnectTimeout = 500;
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
                        metadata.KeepAliveTask = StartProcessWithKeepAlive(metadata, GetMaxRetryInterval(azureAppServiceSettings.AgentMaxRetryIntervalSeconds));
                    }
                }
            }
        }

        private static Task StartProcessWithKeepAlive(ProcessMetadata metadata, int maxRetryInterval)
        {
            var path = metadata.ProcessPath;
            Log.Debug("Starting keep alive for {AgentPath}.", path);

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

                                            // A slow answer is not a shutdown: keep watching while the pipe exists
                                            if (await metadata.ProcessIsHealthyAsync().ConfigureAwait(false) || metadata.PipeIsPresent)
                                            {
                                                // Should result in a max delay of ~3.28 seconds before giving up
                                                delay = delay * 1.75d;
                                                continue;
                                            }

                                            // The previous instance is gone, time to start the process
                                            Log.Information("Recovering from previous {AgentPath} shutdown. Ready for start.", path);
                                            metadata.ProcessState = ProcessState.ReadyToStart;
                                            break;
                                        }

                                        if (metadata.ProcessState == ProcessState.Healthy)
                                        {
                                            Log.Information("{AgentPath} is already running and was not started by the .NET Tracer in this process. Using it.", path);
                                        }
                                    }
                                    else if (metadata.PipeIsPresent)
                                    {
                                        // Count this as the first failed check, so the regular checks wait before starting a copy
                                        metadata.FailedHealthChecks = 1;
                                        metadata.ProcessState = ProcessState.Unhealthy;
                                        LogFailedAgentNotStartedByUs(metadata);
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
                                    var isHealthy = await metadata.ProcessIsHealthyAsync().ConfigureAwait(false);

                                    // Check whether our own process is running only after the health check, so an exit during the
                                    // check is logged and counted. Every decision below uses this one reading.
                                    var ownProcessIsRunning = metadata.OwnProcessIsRunning();
                                    var ownProcessExited = !ownProcessIsRunning && RecordOwnProcessExit(metadata);
                                    metadata.FailedHealthChecks = isHealthy ? 0 : metadata.FailedHealthChecks + 1;
                                    var otherAgentHasPipe = metadata.PipeIsPresent && !ownProcessExited;
                                    var action = GetKeepAliveAction(isHealthy, ownProcessIsRunning, otherAgentHasPipe, metadata.FailedHealthChecks);
                                    if (isHealthy)
                                    {
                                        // Reset the backoff only after StableHealthyPeriod, so an agent that keeps flapping still backs off
                                        metadata.HealthyStopwatch ??= Stopwatch.StartNew();
                                        if (metadata.HealthyStopwatch.ElapsedMilliseconds >= StableHealthyPeriod)
                                        {
                                            metadata.SequentialFailures = 0;
                                        }

                                        metadata.ProcessState = ProcessState.Healthy;
                                        if (previousState == ProcessState.Unhealthy)
                                        {
                                            if (metadata.Process == null)
                                            {
                                                Log.Information("{AgentPath} is now healthy.", path);
                                            }
                                            else
                                            {
                                                Log.Information("{AgentPath} with pid {ChildPid} is now healthy.", path, GetProcessId(metadata.Process));
                                            }
                                        }
                                    }
                                    else if (action == KeepAliveAction.Start)
                                    {
                                        metadata.HealthyStopwatch = null;
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                        var startDelay = GetStartDelay(ownProcessExited, metadata.SequentialFailures, maxRetryInterval);
                                        if (startDelay > 0)
                                        {
                                            // The process we started exited after the startup wait: back off like any other failure
                                            if (metadata.SequentialFailures == 1)
                                            {
                                                Log.Warning("{AgentPath} started by the .NET Tracer in this process is gone ({Reason}). Starting a new one in {Delay} ms ({Failures} failures in a row).", new object[] { path, metadata.LastHealthCheckFailure, startDelay, metadata.SequentialFailures });
                                            }
                                            else
                                            {
                                                Log.Debug("{AgentPath} started by the .NET Tracer in this process is gone ({Reason}). Starting a new one in {Delay} ms ({Failures} failures in a row).", new object[] { path, metadata.LastHealthCheckFailure, startDelay, metadata.SequentialFailures });
                                            }

                                            await Task.Delay(startDelay).ConfigureAwait(false);
                                            metadata.ProcessState = await GetStateAfterRetryDelayAsync(metadata).ConfigureAwait(false);
                                        }
                                        else if (previousState is ProcessState.Healthy or ProcessState.Unhealthy)
                                        {
                                            Log.Information("{AgentPath} is no longer available ({Reason}). Starting a new one.", path, metadata.LastHealthCheckFailure);
                                        }
                                    }
                                    else if (action == KeepAliveAction.Restart)
                                    {
                                        // Only reached for the process we started: we never stop an agent we did not start.
                                        // Stopping is retried on later checks if it fails, so only the first attempt logs above Debug.
                                        metadata.HealthyStopwatch = null;
                                        var firstAttempt = metadata.FailedHealthChecks == MaxFailedHealthChecks;
                                        var childPid = GetProcessId(metadata.Process);
                                        if (TryStopOwnProcess(metadata, firstAttempt))
                                        {
                                            // A restart counts as a failure, so an agent that never becomes healthy backs off too
                                            metadata.SequentialFailures++;
                                            var delay = GetRetryDelay(metadata.SequentialFailures, maxRetryInterval);
                                            if (metadata.SequentialFailures == 1)
                                            {
                                                Log.Warning("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process failed {Count} health checks in a row ({Reason}). Restarting it in {Delay} ms ({Failures} failures in a row).", new object[] { path, childPid, metadata.FailedHealthChecks, metadata.LastHealthCheckFailure, delay, metadata.SequentialFailures });
                                            }
                                            else
                                            {
                                                Log.Debug("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process failed {Count} health checks in a row ({Reason}). Restarting it in {Delay} ms ({Failures} failures in a row).", new object[] { path, childPid, metadata.FailedHealthChecks, metadata.LastHealthCheckFailure, delay, metadata.SequentialFailures });
                                            }

                                            await Task.Delay(delay).ConfigureAwait(false);
                                            metadata.ProcessState = await GetStateAfterRetryDelayAsync(metadata).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            if (firstAttempt)
                                            {
                                                Log.Warning(
                                                    "{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process failed {Count} health checks in a row ({Reason}) and could not be stopped. Retrying.",
                                                    new object[] { path, childPid, metadata.FailedHealthChecks, metadata.LastHealthCheckFailure });
                                            }

                                            metadata.ProcessState = ProcessState.Unhealthy;
                                        }
                                    }
                                    else if (!ownProcessIsRunning)
                                    {
                                        // An agent we did not start still has its pipe: GetKeepAliveAction waits before starting a copy
                                        metadata.HealthyStopwatch = null;
                                        metadata.ProcessState = ProcessState.Unhealthy;
                                        if (previousState != ProcessState.Unhealthy)
                                        {
                                            LogFailedAgentNotStartedByUs(metadata);
                                        }
                                    }
                                    else
                                    {
                                        // The process we started is still running: never start a second copy
                                        metadata.HealthyStopwatch = null;
                                        metadata.ProcessState = ProcessState.Unhealthy;
                                        if (previousState != ProcessState.Unhealthy)
                                        {
                                            Log.Warning("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process is running but failed its health check ({Reason}).", path, GetProcessId(metadata.Process), metadata.LastHealthCheckFailure);
                                        }
                                    }
                                }

                                if (metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    var isRetry = metadata.SequentialFailures > 0;
                                    if (isRetry)
                                    {
                                        Log.Debug("Attempting to start {AgentPath}.", path);
                                    }
                                    else
                                    {
                                        Log.Information("Attempting to start {AgentPath}.", path);
                                    }

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
                                    if (isRetry)
                                    {
                                        Log.Debug("Started {AgentPath} with pid {ChildPid}.", path, childPid);
                                    }
                                    else
                                    {
                                        Log.Information("Started {AgentPath} with pid {ChildPid}.", path, childPid);
                                    }

                                    const int startupTimeout = 2000;
                                    var startupStopwatch = Stopwatch.StartNew();

                                    while (startupStopwatch.ElapsedMilliseconds < startupTimeout)
                                    {
                                        // Loop and watch for evidence that the process is up and running
                                        if (await metadata.ProcessIsHealthyAsync().ConfigureAwait(false))
                                        {
                                            metadata.HealthyStopwatch = Stopwatch.StartNew();
                                            metadata.ProcessState = ProcessState.Healthy;
                                            if (metadata.Process == null || metadata.Process.HasExited)
                                            {
                                                // The health check passed because of another instance, not the one we started
                                                Log.Information(
                                                    "{AgentPath} with pid {ChildPid} exited with code {ExitCode}, but another instance is already running. Using it.",
                                                    path,
                                                    childPid,
                                                    GetExitCode(metadata.Process));
                                                ForgetOwnProcess(metadata);
                                            }
                                            else
                                            {
                                                Log.Information("Successfully started {AgentPath} with pid {ChildPid}.", path, childPid);
                                            }

                                            break;
                                        }

                                        if (metadata.Process == null || metadata.Process.HasExited)
                                        {
                                            // Retried with backoff, so not an Error
                                            if (isRetry)
                                            {
                                                Log.Debug("Failed to start {AgentPath}: pid {ChildPid} exited with code {ExitCode}.", path, childPid, GetExitCode(metadata.Process));
                                            }
                                            else
                                            {
                                                Log.Warning("Failed to start {AgentPath}: pid {ChildPid} exited with code {ExitCode}.", path, childPid, GetExitCode(metadata.Process));
                                            }

                                            ForgetOwnProcess(metadata);
                                            metadata.ProcessState = ProcessState.Faulted;
                                            break;
                                        }

                                        await Task.Delay(100).ConfigureAwait(false);
                                    }

                                    if (metadata.ProcessState == ProcessState.ReadyToStart)
                                    {
                                        if (isRetry)
                                        {
                                            Log.Debug("{AgentPath} with pid {ChildPid} is running but did not pass its health check within {Timeout} ms ({Reason}).", new object[] { path, childPid, startupTimeout, metadata.LastHealthCheckFailure });
                                        }
                                        else
                                        {
                                            Log.Warning("{AgentPath} with pid {ChildPid} is running but did not pass its health check within {Timeout} ms ({Reason}).", new object[] { path, childPid, startupTimeout, metadata.LastHealthCheckFailure });
                                        }

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

                                // Expected failures (the agent exits, fails its health check, or does not start in time) are handled
                                // above without exceptions, so anything that reaches here is unexpected and is logged as an Error.
                                // The retry backoff limits how often a repeating error is logged.
                                Log.Error(ex, "Unexpected error in keep-alive for {AgentPath}.", path);
                            }
                            finally
                            {
                                if (metadata.ProcessState == ProcessState.Faulted)
                                {
                                    metadata.SequentialFailures++;
                                    // Back off while failures repeat
                                    await Task.Delay(GetRetryDelay(metadata.SequentialFailures, maxRetryInterval)).ConfigureAwait(false);
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
                        Log.Error(ex, "Error occurred in keep-alive for {AgentPath}.", path);
                    }
                    finally
                    {
                        Log.Warning("Keep alive is dropping for {AgentPath}.", path);
                        metadata.IsBeingManaged = false;
                    }
                });
        }

        /// <summary>
        /// Decides what the keep-alive does after a health check. The process this keep-alive started is restarted after
        /// <see cref="MaxFailedHealthChecks"/> failed checks in a row. When that process is not running, a new one is started
        /// after the first failed check, unless an agent we did not start still has the pipe: a new copy cannot take the pipe
        /// over, so starting one waits for <see cref="MaxFailedHealthChecks"/> failed checks in a row.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static KeepAliveAction GetKeepAliveAction(bool isHealthy, bool ownProcessIsRunning, bool otherAgentHasPipe, int failedHealthChecks)
        {
            if (isHealthy)
            {
                return KeepAliveAction.None;
            }

            if (!ownProcessIsRunning)
            {
                return otherAgentHasPipe && failedHealthChecks < MaxFailedHealthChecks ? KeepAliveAction.None : KeepAliveAction.Start;
            }

            return failedHealthChecks >= MaxFailedHealthChecks ? KeepAliveAction.Restart : KeepAliveAction.None;
        }

        private static void LogFailedAgentNotStartedByUs(ProcessMetadata metadata)
        {
            Log.Warning<string, string, int>(
                "{AgentPath} was not started by the .NET Tracer in this process and failed its health check ({Reason}). Starting a new one if it fails {Count} checks in a row.",
                metadata.ProcessPath,
                metadata.LastHealthCheckFailure,
                MaxFailedHealthChecks);
        }

        /// <summary>
        /// Kills the process this keep-alive started. Returns false if it is still running afterwards,
        /// in which case it stays tracked so that a second copy is never started next to it.
        /// </summary>
        private static bool TryStopOwnProcess(ProcessMetadata metadata, bool firstAttempt)
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
                if (firstAttempt)
                {
                    Log.Warning<string, int?>(ex, "Error when stopping {AgentPath} with pid {ChildPid}.", metadata.ProcessPath, GetProcessId(process));
                }
                else
                {
                    Log.Debug<string, int?>(ex, "Error when stopping {AgentPath} with pid {ChildPid}.", metadata.ProcessPath, GetProcessId(process));
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
        /// Gets the delay before the next attempt after <paramref name="sequentialFailures"/> failures in a row: <see cref="ExceptionRetryInterval"/>
        /// after the first failure, doubling with each further failure up to <paramref name="maxRetryInterval"/>. Retries never stop.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static int GetRetryDelay(int sequentialFailures, int maxRetryInterval)
        {
            // long, so doubling toward a very large cap cannot overflow into a negative delay
            long delay = ExceptionRetryInterval;
            for (var i = 1; i < sequentialFailures && delay < maxRetryInterval; i++)
            {
                delay *= 2;
            }

            return (int)Math.Min(delay, maxRetryInterval);
        }

        /// <summary>
        /// After a retry wait, another worker may have started a healthy agent: use it instead of starting a duplicate.
        /// </summary>
        private static async Task<ProcessState> GetStateAfterRetryDelayAsync(ProcessMetadata metadata)
        {
            if (!await metadata.ProcessIsHealthyAsync().ConfigureAwait(false))
            {
                return ProcessState.ReadyToStart;
            }

            Log.Information("{AgentPath} is already running and was not started by the .NET Tracer in this process. Using it.", metadata.ProcessPath);

            // Failed checks of the agent we replaced must not count against this one
            metadata.FailedHealthChecks = 0;
            return ProcessState.Healthy;
        }

        /// <summary>
        /// Gets the wait before starting a new agent after a health check. Starting is immediate unless the
        /// process this keep-alive started has just exited, which counts as a failure and backs off.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static int GetStartDelay(bool ownProcessExited, int sequentialFailures, int maxRetryInterval)
            => ownProcessExited ? GetRetryDelay(sequentialFailures, maxRetryInterval) : 0;

        /// <summary>
        /// Converts the configured cap in seconds to milliseconds, without overflowing.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static int GetMaxRetryInterval(int maxRetryIntervalSeconds)
            => maxRetryIntervalSeconds > int.MaxValue / 1000 ? int.MaxValue : Math.Max(ExceptionRetryInterval, maxRetryIntervalSeconds * 1000);

        /// <summary>
        /// Logs and forgets the process this keep-alive started if it has exited since the last check, and counts
        /// that exit as a failure in the shared backoff. Returns true when such an exit was found.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static bool RecordOwnProcessExit(ProcessMetadata metadata)
        {
            var process = metadata.Process;
            if (process == null)
            {
                return false;
            }

            try
            {
                if (!process.HasExited)
                {
                    return false;
                }

                if (metadata.SequentialFailures == 0)
                {
                    Log.Information("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process exited with code {ExitCode}.", metadata.ProcessPath, GetProcessId(process), GetExitCode(process));
                }
                else
                {
                    Log.Debug("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process exited with code {ExitCode}.", metadata.ProcessPath, GetProcessId(process), GetExitCode(process));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when checking whether {AgentPath} exited.", metadata.ProcessPath);
                return false;
            }

            metadata.SequentialFailures++;

            // Forget the process so this exit is reported and counted only once
            ForgetOwnProcess(metadata);
            return true;
        }

        private static void ForgetOwnProcess(ProcessMetadata metadata)
        {
            var process = metadata.Process;
            metadata.Process = null;
            process?.Dispose();
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

        private static int? GetProcessId(Process process)
        {
            try
            {
                return process?.Id;
            }
            catch
            {
                return null;
            }
        }

        private static int? GetExitCode(Process process)
        {
            try
            {
                return process?.ExitCode;
            }
            catch
            {
                return null;
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

            /// <summary>
            /// Gets or sets the number of failures in a row, which sets the retry backoff: failed starts, exits of the process
            /// we started, restarts, and unexpected exceptions. Only the first failed start, exit, or restart in a run logs above
            /// Debug, because later ones are retries. It resets after the agent stays healthy for <see cref="StableHealthyPeriod"/>.
            /// </summary>
            public int SequentialFailures { get; set; }

            public int FailedHealthChecks { get; set; }

            /// <summary>
            /// Gets or sets how long the agent has been healthy without a failed check, or null while it is not healthy.
            /// </summary>
            public Stopwatch HealthyStopwatch { get; set; }

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

            /// <summary>
            /// Gets or sets why the last health check failed, for the state-change logs, or null if it passed.
            /// </summary>
            public string LastHealthCheckFailure { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the agent's named pipe existed at the last health check.
            /// </summary>
            public bool PipeIsPresent { get; set; }

            public bool OwnProcessIsRunning()
            {
                try
                {
                    return Process != null && !Process.HasExited;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error when checking whether {AgentPath} is running.", ProcessPath);
                    return false;
                }
            }

            /// <summary>
            /// Checks whether the agent is healthy, whoever started it: its named pipe must exist, and the trace
            /// agent must also answer GET /info. Sets <see cref="PipeIsPresent"/>, and <see cref="LastHealthCheckFailure"/> when it is not healthy.
            /// Whether the process this keep-alive started is still running is checked by <see cref="OwnProcessIsRunning"/>.
            /// </summary>
            public async Task<bool> ProcessIsHealthyAsync()
            {
                PipeIsPresent = NamedPipeIsBound();
                if (!PipeIsPresent)
                {
                    LastHealthCheckFailure = $"named pipe {PipeName} is not present";
                    return false;
                }

                // DogStatsD never replies, so its pipe being present is all we can check
                LastHealthCheckFailure = IsTraceAgent ? await GetTraceAgentFailureAsync().ConfigureAwait(false) : null;
                return LastHealthCheckFailure == null;
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
            /// Returns null if the agent answered, otherwise why it did not.
            /// </summary>
            private async Task<string> GetTraceAgentFailureAsync()
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

                // Disposing the pipe on the way out aborts the pending exchange
                return $"no response to GET /info on named pipe {PipeName} within {TraceAgentResponseTimeout} ms";
            }

            private async Task<string> SendInfoRequestAsync(NamedPipeClientStream pipe)
            {
                try
                {
                    try
                    {
                        await pipe.ConnectAsync(TraceAgentConnectTimeout).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        return $"could not connect to named pipe {PipeName} within {TraceAgentConnectTimeout} ms";
                    }

                    await pipe.WriteAsync(TraceAgentInfoRequest, 0, TraceAgentInfoRequest.Length).ConfigureAwait(false);

                    // Any status line means the agent is serving requests
                    var statusLineStart = new byte[5];
                    var read = 0;
                    while (read < statusLineStart.Length)
                    {
                        var count = await pipe.ReadAsync(statusLineStart, read, statusLineStart.Length - read).ConfigureAwait(false);
                        if (count == 0)
                        {
                            return $"named pipe {PipeName} closed before a response to GET /info";
                        }

                        read += count;
                    }

                    return Encoding.ASCII.GetString(statusLineStart) == "HTTP/" ? null : $"unexpected response to GET /info on named pipe {PipeName}";
                }
                catch (Exception ex)
                {
                    // Every probe can fail during an outage: keep the details out of default-level logs.
                    // The state-change logs carry the reason.
                    Log.Debug(ex, "Health check request to the trace agent failed on named pipe {PipeName}.", PipeName);
                    return $"{ex.GetType().Name} on named pipe {PipeName}: {ex.Message}";
                }
            }
        }
    }
}
