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
using Datadog.Trace.Vendors.Serilog.Events;

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
        private const int StartupTimeout = 2_000;

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

        private static readonly byte[] HttpStatusLineStart = Encoding.ASCII.GetBytes("HTTP/");

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
        /// What the keep-alive uses from outside this class: the agent's health check, waits, and the time.
        /// <see cref="AgentHost"/> is the real one, and tests pass a fake. The keep-alive takes it as a type
        /// parameter constrained to a struct, so the JIT calls it directly, without delegates or allocations.
        /// </summary>
        internal interface IAgentHost
        {
            /// <summary>
            /// Checks whether the agent is healthy, and sets <see cref="ProcessMetadata.PipeIsPresent"/> and
            /// <see cref="ProcessMetadata.LastHealthCheckFailure"/> as <see cref="ProcessMetadata.ProcessIsHealthyAsync"/> does.
            /// </summary>
            Task<bool> CheckHealthAsync(ProcessMetadata metadata);

            Task DelayAsync(int milliseconds);

            /// <summary>
            /// Gets a monotonic time in milliseconds, for measuring how long something took.
            /// </summary>
            long GetTimestampMs();
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
            Log.Debug("Starting keep alive for {AgentPath}.", metadata.ProcessPath);
            return Task.Run(() => KeepAliveAsync(default(AgentHost), metadata, maxRetryInterval));
        }

        /// <summary>
        /// Checks the agent in a loop and starts or restarts it when needed. Each pass runs one check, starts the agent
        /// if the check leaves it <see cref="ProcessState.ReadyToStart"/>, then waits: <see cref="KeepAliveInterval"/>
        /// normally, or the retry backoff after a failure.
        /// </summary>
        private static async Task KeepAliveAsync<THost>(THost host, ProcessMetadata metadata, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var path = metadata.ProcessPath;
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
                            await CheckForExistingAgentAsync(host, metadata, maxRetryInterval).ConfigureAwait(false);
                        }
                        else
                        {
                            await CheckAgentAsync(host, metadata, maxRetryInterval).ConfigureAwait(false);
                        }

                        if (metadata.ProcessState == ProcessState.ReadyToStart)
                        {
                            await StartAgentAsync(host, metadata, maxRetryInterval).ConfigureAwait(false);
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
                        // without exceptions, so anything that reaches here is unexpected and is logged as an Error.
                        // The retry backoff limits how often a repeating error is logged.
                        Log.Error(ex, "Unexpected error in keep-alive for {AgentPath}.", path);
                    }
                    finally
                    {
                        if (metadata.ProcessState == ProcessState.Faulted)
                        {
                            metadata.SequentialFailures++;
                            // Back off while failures repeat
                            await host.DelayAsync(GetRetryDelay(metadata.SequentialFailures, maxRetryInterval)).ConfigureAwait(false);
                        }
                        else
                        {
                            // Delay for a reasonable amount of time before we check to see if the process is alive again.
                            await host.DelayAsync(KeepAliveInterval).ConfigureAwait(false);
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
        }

        /// <summary>
        /// The first check from this keep-alive. An agent that answers was started by another worker or domain, or it is
        /// a previous instance that is shutting down, so it is watched briefly before it is used.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static async Task CheckForExistingAgentAsync<THost>(THost host, ProcessMetadata metadata, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var path = metadata.ProcessPath;
            if (await host.CheckHealthAsync(metadata).ConfigureAwait(false))
            {
                if (await ExistingAgentStaysAsync(host, metadata).ConfigureAwait(false))
                {
                    metadata.ProcessState = ProcessState.Healthy;
                    Log.Information("{AgentPath} is already running and was not started by the .NET Tracer in this process. Using it.", path);
                }
                else
                {
                    Log.Information("Recovering from previous {AgentPath} shutdown. Ready for start.", path);
                    metadata.ProcessState = ProcessState.ReadyToStart;
                }
            }
            else if (metadata.PipeIsPresent)
            {
                // Count this as the first failed check, so the regular checks wait before starting a copy
                metadata.FailedHealthChecks = 1;
                metadata.ProcessState = ProcessState.Unhealthy;
                LogFailedAgentNotStartedByUs(metadata, IsRetryLoggedAboveDebug(metadata.SequentialFailures + 1, maxRetryInterval));
            }
            else
            {
                // If no instance exists, just get ready to start
                metadata.ProcessState = ProcessState.ReadyToStart;
            }
        }

        /// <summary>
        /// Watches an agent found by the first check for about 2 seconds, with growing gaps between checks.
        /// Returns false if its pipe goes away, which means it was shutting down. A slow answer is not a shutdown,
        /// so the agent counts as staying while its pipe exists.
        /// </summary>
        private static async Task<bool> ExistingAgentStaysAsync<THost>(THost host, ProcessMetadata metadata)
            where THost : struct, IAgentHost
        {
            var delay = 50d;
            for (var check = 0; check < 6; check++)
            {
                await host.DelayAsync((int)delay).ConfigureAwait(false);
                if (!await host.CheckHealthAsync(metadata).ConfigureAwait(false) && !metadata.PipeIsPresent)
                {
                    return false;
                }

                delay *= 1.75d;
            }

            return true;
        }

        /// <summary>
        /// A regular check after the first one. One health check and one reading of our own process decide whether the
        /// agent is healthy, whether to start a new one, restart ours, or wait for more failed checks.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static async Task CheckAgentAsync<THost>(THost host, ProcessMetadata metadata, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var previousState = metadata.ProcessState;
            var isHealthy = await host.CheckHealthAsync(metadata).ConfigureAwait(false);

            // Check whether our own process is running only after the health check, so an exit during the
            // check is logged and counted. Every decision below uses this one reading.
            var ownProcessIsRunning = metadata.OwnProcessIsRunning();
            var ownProcessExited = !ownProcessIsRunning && RecordOwnProcessExit(metadata);
            var otherAgentHasPipe = metadata.PipeIsPresent && !ownProcessExited;
            metadata.FailedHealthChecks = isHealthy ? 0 : metadata.FailedHealthChecks + 1;
            var action = GetKeepAliveAction(isHealthy, ownProcessIsRunning, otherAgentHasPipe, metadata.FailedHealthChecks);

            if (isHealthy)
            {
                MarkHealthy(host, metadata, previousState);
                return;
            }

            metadata.HealthySinceMs = null;
            switch (action)
            {
                case KeepAliveAction.Start:
                    await PrepareToStartAsync(host, metadata, previousState, ownProcessExited, maxRetryInterval).ConfigureAwait(false);
                    break;
                case KeepAliveAction.Restart:
                    await RestartOwnProcessAsync(host, metadata, maxRetryInterval).ConfigureAwait(false);
                    break;
                default:
                    MarkUnhealthy(metadata, previousState, ownProcessIsRunning, maxRetryInterval);
                    break;
            }
        }

        private static void MarkHealthy<THost>(THost host, ProcessMetadata metadata, ProcessState previousState)
            where THost : struct, IAgentHost
        {
            // Reset the backoff only after StableHealthyPeriod, so an agent that keeps flapping still backs off
            var now = host.GetTimestampMs();
            metadata.HealthySinceMs ??= now;
            if (now - metadata.HealthySinceMs.Value >= StableHealthyPeriod)
            {
                metadata.SequentialFailures = 0;
            }

            metadata.ProcessState = ProcessState.Healthy;
            if (previousState != ProcessState.Unhealthy)
            {
                return;
            }

            if (metadata.Process == null)
            {
                Log.Information("{AgentPath} is now healthy.", metadata.ProcessPath);
            }
            else
            {
                Log.Information("{AgentPath} with pid {ChildPid} is now healthy.", metadata.ProcessPath, GetProcessId(metadata.Process));
            }
        }

        /// <summary>
        /// Gets ready to start a new agent. After an exit of the process we started, first waits for the retry backoff,
        /// then uses an agent another worker started in the meantime if there is one.
        /// </summary>
        private static async Task PrepareToStartAsync<THost>(THost host, ProcessMetadata metadata, ProcessState previousState, bool ownProcessExited, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var path = metadata.ProcessPath;
            var startDelay = GetStartDelay(ownProcessExited, metadata.SequentialFailures, maxRetryInterval);
            metadata.ProcessState = ProcessState.ReadyToStart;
            if (startDelay > 0)
            {
                if (IsRetryLoggedAboveDebug(metadata.SequentialFailures, maxRetryInterval))
                {
                    Log.Warning("{AgentPath} started by the .NET Tracer in this process is gone ({Reason}). Starting a new one in {Delay} ms ({Failures} failures in a row).", new object[] { path, metadata.LastHealthCheckFailure, startDelay, metadata.SequentialFailures });
                }
                else
                {
                    Log.Debug<string, string, int, int>("{AgentPath} started by the .NET Tracer in this process is gone ({Reason}). Starting a new one in {Delay} ms ({Failures} failures in a row).", path, metadata.LastHealthCheckFailure, startDelay, metadata.SequentialFailures);
                }

                await host.DelayAsync(startDelay).ConfigureAwait(false);
                metadata.ProcessState = await GetStateAfterRetryDelayAsync(host, metadata).ConfigureAwait(false);
            }
            else if (previousState == ProcessState.Healthy
                  || (previousState == ProcessState.Unhealthy && IsRetryLoggedAboveDebug(metadata.SequentialFailures + 1, maxRetryInterval)))
            {
                Log.Information("{AgentPath} is no longer available ({Reason}). Starting a new one.", path, metadata.LastHealthCheckFailure);
            }
            else if (previousState == ProcessState.Unhealthy)
            {
                Log.Debug("{AgentPath} is no longer available ({Reason}). Starting a new one.", path, metadata.LastHealthCheckFailure);
            }
        }

        /// <summary>
        /// Stops the process we started after <see cref="MaxFailedHealthChecks"/> failed checks in a row, waits for the
        /// retry backoff, then gets ready to start a new one. We never stop an agent we did not start. If our process
        /// cannot be stopped, it stays tracked and stopping is retried on later checks.
        /// </summary>
        private static async Task RestartOwnProcessAsync<THost>(THost host, ProcessMetadata metadata, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var path = metadata.ProcessPath;

            // Only the first attempt logs above Debug
            var firstAttempt = metadata.FailedHealthChecks == MaxFailedHealthChecks;
            var childPid = GetProcessId(metadata.Process);
            if (!TryStopOwnProcess(metadata, firstAttempt))
            {
                if (firstAttempt)
                {
                    Log.Warning(
                        "{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process failed {Count} health checks in a row ({Reason}) and could not be stopped. Retrying.",
                        new object[] { path, childPid, metadata.FailedHealthChecks, metadata.LastHealthCheckFailure });
                }

                metadata.ProcessState = ProcessState.Unhealthy;
                return;
            }

            // A restart counts as a failure, so an agent that never becomes healthy backs off too
            metadata.SequentialFailures++;
            var delay = GetRetryDelay(metadata.SequentialFailures, maxRetryInterval);
            if (IsRetryLoggedAboveDebug(metadata.SequentialFailures, maxRetryInterval))
            {
                Log.Warning("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process failed {Count} health checks in a row ({Reason}). Restarting it in {Delay} ms ({Failures} failures in a row).", new object[] { path, childPid, metadata.FailedHealthChecks, metadata.LastHealthCheckFailure, delay, metadata.SequentialFailures });
            }
            else if (Log.IsEnabled(LogEventLevel.Debug))
            {
                // Six values have no generic overload, so check the level before allocating the array
                Log.Debug("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process failed {Count} health checks in a row ({Reason}). Restarting it in {Delay} ms ({Failures} failures in a row).", new object[] { path, childPid, metadata.FailedHealthChecks, metadata.LastHealthCheckFailure, delay, metadata.SequentialFailures });
            }

            await host.DelayAsync(delay).ConfigureAwait(false);
            metadata.ProcessState = await GetStateAfterRetryDelayAsync(host, metadata).ConfigureAwait(false);
        }

        /// <summary>
        /// The agent failed its health check, but it is not yet time to start or restart one. Logs only when the agent
        /// becomes unhealthy, not on every failed check.
        /// </summary>
        private static void MarkUnhealthy(ProcessMetadata metadata, ProcessState previousState, bool ownProcessIsRunning, int maxRetryInterval)
        {
            metadata.ProcessState = ProcessState.Unhealthy;
            if (previousState == ProcessState.Unhealthy)
            {
                return;
            }

            if (ownProcessIsRunning)
            {
                // The process we started is still running: never start a second copy
                Log.Warning("{AgentPath} with pid {ChildPid} started by the .NET Tracer in this process is running but failed its health check ({Reason}).", metadata.ProcessPath, GetProcessId(metadata.Process), metadata.LastHealthCheckFailure);
            }
            else
            {
                // An agent we did not start still has its pipe: GetKeepAliveAction waits before starting a copy
                LogFailedAgentNotStartedByUs(metadata, IsRetryLoggedAboveDebug(metadata.SequentialFailures + 1, maxRetryInterval));
            }
        }

        /// <summary>
        /// Starts a new agent process, then waits for it with <see cref="WaitForStartedAgentAsync"/>.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static async Task StartAgentAsync<THost>(THost host, ProcessMetadata metadata, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var path = metadata.ProcessPath;

            // If this attempt fails, it is counted as failure SequentialFailures + 1
            var logAboveDebug = IsRetryLoggedAboveDebug(metadata.SequentialFailures + 1, maxRetryInterval);
            if (logAboveDebug)
            {
                Log.Information("Attempting to start {AgentPath}.", path);
            }
            else
            {
                Log.Debug("Attempting to start {AgentPath}.", path);
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
            if (logAboveDebug)
            {
                Log.Information("Started {AgentPath} with pid {ChildPid}.", path, childPid);
            }
            else
            {
                Log.Debug("Started {AgentPath} with pid {ChildPid}.", path, childPid);
            }

            metadata.ProcessState = await WaitForStartedAgentAsync(host, metadata, childPid, logAboveDebug, maxRetryInterval).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits up to <see cref="StartupTimeout"/> for a just-started agent to pass a health check. Returns Healthy when
        /// it does (or another instance does), Faulted when our process exited first so the retry backoff applies, or
        /// Unhealthy when it is still running but not ready, so the regular checks take over.
        /// </summary>
        private static async Task<ProcessState> WaitForStartedAgentAsync<THost>(THost host, ProcessMetadata metadata, int? childPid, bool logAboveDebug, int maxRetryInterval)
            where THost : struct, IAgentHost
        {
            var path = metadata.ProcessPath;
            var startedAt = host.GetTimestampMs();
            while (host.GetTimestampMs() - startedAt < StartupTimeout)
            {
                if (await host.CheckHealthAsync(metadata).ConfigureAwait(false))
                {
                    metadata.HealthySinceMs = host.GetTimestampMs();
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

                    return ProcessState.Healthy;
                }

                if (metadata.Process == null || metadata.Process.HasExited)
                {
                    // Retried with backoff, so not an Error
                    var failures = metadata.SequentialFailures + 1;
                    var retryDelay = GetRetryDelay(failures, maxRetryInterval);
                    if (logAboveDebug)
                    {
                        Log.Warning("Failed to start {AgentPath}: pid {ChildPid} exited with code {ExitCode}. Retrying in {Delay} ms ({Failures} failures in a row).", new object[] { path, childPid, GetExitCode(metadata.Process), retryDelay, failures });
                    }
                    else
                    {
                        Log.Debug<string, int?, int?, int, int>("Failed to start {AgentPath}: pid {ChildPid} exited with code {ExitCode}. Retrying in {Delay} ms ({Failures} failures in a row).", path, childPid, GetExitCode(metadata.Process), retryDelay, failures);
                    }

                    ForgetOwnProcess(metadata);
                    return ProcessState.Faulted;
                }

                await host.DelayAsync(100).ConfigureAwait(false);
            }

            if (logAboveDebug)
            {
                Log.Warning("{AgentPath} with pid {ChildPid} is running but did not pass its health check within {Timeout} ms ({Reason}).", new object[] { path, childPid, StartupTimeout, metadata.LastHealthCheckFailure });
            }
            else
            {
                Log.Debug<string, int?, int, string>("{AgentPath} with pid {ChildPid} is running but did not pass its health check within {Timeout} ms ({Reason}).", path, childPid, StartupTimeout, metadata.LastHealthCheckFailure);
            }

            return ProcessState.Unhealthy;
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

        private static void LogFailedAgentNotStartedByUs(ProcessMetadata metadata, bool aboveDebug)
        {
            if (aboveDebug)
            {
                Log.Warning<string, string, int>("{AgentPath} was not started by the .NET Tracer in this process and failed its health check ({Reason}). Starting a new one if it fails {Count} checks in a row.", metadata.ProcessPath, metadata.LastHealthCheckFailure, MaxFailedHealthChecks);
            }
            else
            {
                Log.Debug<string, string, int>("{AgentPath} was not started by the .NET Tracer in this process and failed its health check ({Reason}). Starting a new one if it fails {Count} checks in a row.", metadata.ProcessPath, metadata.LastHealthCheckFailure, MaxFailedHealthChecks);
            }
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
        /// Decides whether a retry after <paramref name="sequentialFailures"/> failures in a row logs above Debug. The first
        /// failure does, and so does every retry once the wait has reached <paramref name="maxRetryInterval"/>, so a lasting
        /// failure stays visible at the default log level about once per <paramref name="maxRetryInterval"/>.
        /// </summary>
        [TestingAndPrivateOnly]
        internal static bool IsRetryLoggedAboveDebug(int sequentialFailures, int maxRetryInterval)
            => sequentialFailures <= 1 || GetRetryDelay(sequentialFailures, maxRetryInterval) >= maxRetryInterval;

        /// <summary>
        /// After a retry wait, another worker may have started a healthy agent: use it instead of starting a duplicate.
        /// </summary>
        private static async Task<ProcessState> GetStateAfterRetryDelayAsync<THost>(THost host, ProcessMetadata metadata)
            where THost : struct, IAgentHost
        {
            if (!await host.CheckHealthAsync(metadata).ConfigureAwait(false))
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

        /// <summary>
        /// The real <see cref="IAgentHost"/>: the health check over the agent's pipe, <see cref="Task.Delay(int)"/>, and <see cref="Stopwatch"/> time.
        /// </summary>
        internal readonly struct AgentHost : IAgentHost
        {
            public Task<bool> CheckHealthAsync(ProcessMetadata metadata) => metadata.ProcessIsHealthyAsync();

            public Task DelayAsync(int milliseconds) => Task.Delay(milliseconds);

            public long GetTimestampMs() => (long)StopwatchHelpers.GetElapsedMilliseconds(Stopwatch.GetTimestamp());
        }

        internal sealed class ProcessMetadata
        {
            private string _processPath;
            private string _pipeName;
            private string _pipePath;

            public string PipeName
            {
                get => _pipeName;
                set
                {
                    _pipeName = value;
                    _pipePath = null;
                }
            }

            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is being managed by active keep alive tasks.
            /// </summary>
            public bool IsBeingManaged { get; set; }

            /// <summary>
            /// Gets or sets the number of failures in a row, which sets the retry backoff: failed starts, exits of the process
            /// we started, restarts, and unexpected exceptions. <see cref="IsRetryLoggedAboveDebug"/> decides which retry logs go
            /// above Debug. It resets after the agent stays healthy for <see cref="StableHealthyPeriod"/>.
            /// </summary>
            public int SequentialFailures { get; set; }

            public int FailedHealthChecks { get; set; }

            /// <summary>
            /// Gets or sets when the agent became healthy, from <see cref="IAgentHost.GetTimestampMs"/>, or null while it is not healthy.
            /// A failed check clears it.
            /// </summary>
            public long? HealthySinceMs { get; set; }

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
                // Built once: the health check runs every KeepAliveInterval for as long as the app runs
                var namedPipe = _pipePath ??= $"\\\\.\\pipe\\{PipeName}";
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
                    var statusLineStart = new byte[HttpStatusLineStart.Length];
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

                    return statusLineStart.AsSpan().SequenceEqual(HttpStatusLineStart) ? null : $"unexpected response to GET /info on named pipe {PipeName}";
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
