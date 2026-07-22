// <copyright file="AgentProcessManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        internal const int MaxFailures = 5;

        internal static readonly ProcessMetadata TraceAgentMetadata = new ProcessMetadata
        {
            Name = "datadog-trace-agent",
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

        internal enum ProcessState
        {
            NeverChecked,
            ReadyToStart,
            Faulted,
            Healthy
        }

        internal static bool EvaluateHealth(bool pipeBound, bool programRunning, bool requirePipeBound)
        {
            return requirePipeBound ? pipeBound : (pipeBound || programRunning);
        }

        // Pure debounce decision for a pipe-only process, split out so it can be unit tested without
        // touching the filesystem or enumerating processes. Returns whether the process should be
        // treated as healthy and the updated consecutive-unbound-pipe count:
        // - a bound pipe is healthy and resets the count;
        // - an unbound pipe on a process that is no longer running is unhealthy now (no grace);
        // - an unbound pipe on a still-running process is tolerated until the grace count is reached.
        internal static DebouncedHealth EvaluateDebouncedHealth(bool pipeBound, bool programRunning, int consecutiveUnboundChecks)
        {
            if (pipeBound)
            {
                return new DebouncedHealth(isHealthy: true, nextUnboundCount: 0);
            }

            if (!programRunning)
            {
                return new DebouncedHealth(isHealthy: false, nextUnboundCount: consecutiveUnboundChecks);
            }

            var nextCount = consecutiveUnboundChecks + 1;
            return new DebouncedHealth(isHealthy: nextCount < ProcessMetadata.UnboundPipeGraceChecks, nextUnboundCount: nextCount);
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
                    // A pipe-only dogstatsd (named pipe transport selected) can stay alive but fully
                    // non-functional after failing to bind its pipe, so process liveness is not a
                    // reliable health signal. Resolve the transport from configuration rather than
                    // re-deriving it, so this matches whatever the metrics client will actually use.
                    var exporterSettings = new ExporterSettings(GlobalConfigurationSource.Instance, File.Exists, NullConfigurationTelemetry.Instance);
                    DogStatsDMetadata.RequirePipeBound = exporterSettings.MetricsTransport == Vendors.StatsdClient.Transport.TransportType.NamedPipe;

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
                            if (metadata.SequentialFailures >= MaxFailures)
                            {
                                Log.Error<string, int>("Maximum retries ({ErrorCount}) reached starting {Process}.", path, MaxFailures);
                                metadata.ProcessState = ProcessState.Faulted;
                                return;
                            }

                            try
                            {
                                if (metadata.ProcessState == ProcessState.NeverChecked)
                                {
                                    // This means we have never tried from this domain

                                    if (metadata.IsHealthyDebounced())
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

                                            if (metadata.IsHealthyDebounced())
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
                                    }
                                    else
                                    {
                                        // If no instance exists, just get ready to start
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                    }
                                }
                                else if (metadata.ProcessState == ProcessState.Healthy || metadata.ProcessState == ProcessState.Faulted || metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    // This means we have tried to start from this domain before. ReadyToStart is
                                    // included so a pipe-only process that outran its start-confirmation budget in a
                                    // prior iteration but has since bound its pipe is promoted back to Healthy instead
                                    // of being killed and restarted.
                                    metadata.ProcessState = metadata.IsHealthyDebounced() ? ProcessState.Healthy : ProcessState.ReadyToStart;
                                }

                                if (metadata.ProcessState == ProcessState.ReadyToStart && metadata.RequirePipeBound && metadata.NamedPipeIsBound())
                                {
                                    // The pipe bound between the health re-check above and now, so the tracked
                                    // instance is healthy after all. Promote it and reset the grace count instead
                                    // of starting a redundant duplicate that could never bind the already-held pipe.
                                    metadata.MarkHealthy();
                                }
                                else if (metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    // For a pipe-only process the tracked instance may still be alive but broken
                                    // (it failed to bind its pipe). Kill it before restarting so we don't leak
                                    // our own broken instances across restart cycles. Scoped to metadata.Process,
                                    // the instance this manager started, so it can never touch another worker's process.
                                    if (metadata.RequirePipeBound && metadata.Process is { HasExited: false } stale)
                                    {
                                        try
                                        {
                                            Log.Information<string, int>("Killing broken {Process} (pid {Pid}) before restart; its named pipe never bound.", path, stale.Id);
                                            metadata.KillTrackedProcess();
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Warning(ex, "Failed to kill stale {Process} before restart.", path);
                                        }
                                    }

                                    Log.Information("Attempting to start {Process}.", path);

                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = path, UseShellExecute = false // Force consistency in behavior between Framework and Core
                                    };

                                    if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                    {
                                        startInfo.Arguments = metadata.ProcessArguments;
                                    }

                                    // Reset the grace count for the fresh instance so it gets a full window to
                                    // ride out transient unbound-pipe reads before we consider killing it again.
                                    // A pipe-only instance that stays alive but never binds is intentionally retried
                                    // indefinitely (no circuit breaker): once the process holding the pipe goes away
                                    // a restart binds and metrics recover, which a tripped breaker would prevent.
                                    metadata.ConsecutiveUnboundPipeChecks = 0;

                                    metadata.Process = Process.Start(startInfo);
                                    var timeout = 2000;

                                    while (timeout > 0)
                                    {
                                        // Loop and watch for evidence that the process is up and running
                                        if (metadata.ProcessIsHealthy())
                                        {
                                            metadata.SequentialFailures = 0;
                                            metadata.MarkHealthy();
                                            Log.Information("Successfully started {Process}.", path);
                                            break;
                                        }

                                        if (metadata.Process == null || metadata.Process.HasExited)
                                        {
                                            Log.Error("Failed to start {Process}.", path);
                                            metadata.ProcessState = ProcessState.Faulted;
                                            break;
                                        }

                                        await Task.Delay(100).ConfigureAwait(false);
                                        timeout -= 100;
                                    }
                                }
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
                                    // Quicker retry in these cases
                                    await Task.Delay(ExceptionRetryInterval).ConfigureAwait(false);
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

        // ValueTuple is unavailable on net461, so the debounce result is expressed as an explicit
        // readonly struct. Deconstruct keeps `var (isHealthy, next) = ...` call sites working.
        internal readonly struct DebouncedHealth
        {
            public DebouncedHealth(bool isHealthy, int nextUnboundCount)
            {
                IsHealthy = isHealthy;
                NextUnboundCount = nextUnboundCount;
            }

            public bool IsHealthy { get; }

            public int NextUnboundCount { get; }

            public void Deconstruct(out bool isHealthy, out int nextUnboundCount)
            {
                isHealthy = IsHealthy;
                nextUnboundCount = NextUnboundCount;
            }
        }

        internal sealed class ProcessMetadata
        {
            // Number of consecutive unbound-pipe checks tolerated before a pipe-only process is
            // declared unhealthy, to ride out transient File.Exists blips on the named pipe.
            internal const int UnboundPipeGraceChecks = 2;

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

            /// <summary>
            /// Gets or sets a value indicating whether the process is only healthy when its named pipe
            /// is bound (a pipe-only process that fails to bind stays alive but non-functional).
            /// </summary>
            public bool RequirePipeBound { get; set; }

            public int ConsecutiveUnboundPipeChecks { get; set; }

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

            public void MarkHealthy()
            {
                ProcessState = ProcessState.Healthy;
                ConsecutiveUnboundPipeChecks = 0;
            }

            // Tears down the instance this manager started so we don't leak our own broken instances
            // across restart cycles: kill it if still running, wait briefly for exit, release the handle,
            // and clear the tracked reference so the next restart reassigns Process. An already-exited
            // process still owns an undisposed handle, so it is disposed and cleared too. No-op if nothing
            // is tracked.
            public void KillTrackedProcess()
            {
                if (Process is not { } tracked)
                {
                    return;
                }

                if (!tracked.HasExited)
                {
                    tracked.Kill();
                    tracked.WaitForExit(2000);
                }

                tracked.Dispose();
                Process = null;
            }

            public bool ProcessIsHealthy()
            {
                // Named pipe can return false in some circumstances while it is being written to, so
                // process liveness is a fallback - except for a pipe-only process, which stays alive but
                // non-functional when it fails to bind its pipe (see EvaluateHealth).
                return EvaluateHealth(NamedPipeIsBound(), ProgramIsRunning(), RequirePipeBound);
            }

            // Steady-state health check that debounces transient unbound-pipe reads for pipe-only processes.
            public bool IsHealthyDebounced()
            {
                if (!RequirePipeBound)
                {
                    return ProcessIsHealthy();
                }

                // The grace period only rides out transient File.Exists blips while the process is
                // alive. A pipe-only process that has actually exited is unhealthy now, so restart
                // immediately instead of waiting out a keep-alive cycle. The decision itself is a pure
                // helper (see EvaluateDebouncedHealth) so it can be unit tested without the filesystem.
                var pipeBound = NamedPipeIsBound();
                var (isHealthy, nextUnboundCount) = EvaluateDebouncedHealth(pipeBound, ProgramIsRunning(), ConsecutiveUnboundPipeChecks);
                ConsecutiveUnboundPipeChecks = nextUnboundCount;
                return isHealthy;
            }

            private bool ProgramIsRunning()
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(ProcessPath))
                    {
                        return false;
                    }

                    // fileName will match either TraceAgentMetadata.Name or DogStatsDMetadata.Name
                    var fileName = Path.GetFileNameWithoutExtension(ProcessPath);
                    var processesByName = Process.GetProcessesByName(fileName);

                    if (processesByName.Length > 0)
                    {
                        // We enforce a unique enough naming within contexts where we would use child processes
                        return true;
                    }

                    Log.Information("Program [{Process}] is no longer running", ProcessPath);

                    return false;
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Error when checking for running program {Process}.", ProcessPath);
                    }
                    catch
                    {
                        // ignore, to be safe
                    }

                    return false;
                }
            }

            internal bool NamedPipeIsBound()
            {
                var namedPipe = $"\\\\.\\pipe\\{PipeName}";
                var namedPipeIsBound = File.Exists(namedPipe);
                if (!namedPipeIsBound)
                {
                    Log.Debug("NamedPipe  [{NamedPipe}] is not present.", namedPipe);
                }

                return namedPipeIsBound;
            }
        }
    }
}
