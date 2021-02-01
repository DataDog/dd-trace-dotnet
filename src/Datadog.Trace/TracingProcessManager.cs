using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// This class is used to manage agent processes in contexts where the user can not, such as Azure App Services.
    /// </summary>
    internal class TracingProcessManager
    {
        internal static readonly int KeepAliveInterval = 10_000;
        internal static readonly int ExceptionRetryInterval = 2_000;
        internal static readonly int MaxFailures = 5;

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

        private static readonly List<ProcessMetadata> Processes = new List<ProcessMetadata>()
        {
            TraceAgentMetadata,
            DogStatsDMetadata
        };

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracingProcessManager>();

        internal enum ProcessState
        {
            NeverChecked,
            ReadyToStart,
            Faulted,
            Healthy
        }

        public static void Initialize()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TraceAgentMetadata.ProcessPath))
                {
                    return;
                }

                if (DomainMetadata.ShouldAvoidAppDomain())
                {
                    Log.Information("Skipping process manager initialization for AppDomain: {AppDomain}", DomainMetadata.AppDomainName);
                    return;
                }

                if (!Directory.Exists(TraceAgentMetadata.DirectoryPath))
                {
                    Log.Warning("Directory for trace agent does not exist: {Directory}", TraceAgentMetadata.DirectoryPath);
                    return;
                }

                Log.Debug("Starting child processes from process {ProcessName}, AppDomain {AppDomain}.", DomainMetadata.ProcessName, DomainMetadata.AppDomainName);
                StartProcesses();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when attempting to initialize process manager.");
            }
        }

        private static void StartProcesses()
        {
            if (AzureAppServices.Metadata.DebugModeEnabled)
            {
                const string ddLogLevelKey = "DD_LOG_LEVEL";
                if (EnvironmentHelpers.GetEnvironmentVariable(ddLogLevelKey) == null)
                {
                    // This ensures that a single setting from applicationConfig can enable debug logs for every aspect of the extension
                    EnvironmentHelpers.SetEnvironmentVariable(ddLogLevelKey, "debug");
                }
            }

            foreach (var metadata in Processes)
            {
                if (!string.IsNullOrWhiteSpace(metadata.ProcessPath))
                {
                    if (!metadata.IsBeingManaged)
                    {
                        metadata.KeepAliveTask = StartProcessWithKeepAlive(metadata);
                    }
                }
                else
                {
                    Log.Debug("There is no path configured for {ProcessName}.", metadata.Name);
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

                                    if (metadata.ProcessIsHealthy())
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

                                            if (metadata.ProcessIsHealthy())
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
                                else if (metadata.ProcessState == ProcessState.Healthy || metadata.ProcessState == ProcessState.Faulted)
                                {
                                    // This means we have tried to start from this domain before
                                    metadata.ProcessState = metadata.ProcessIsHealthy() ? ProcessState.Healthy : ProcessState.ReadyToStart;
                                }

                                if (metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    Log.Information("Attempting to start {Process}.", path);

                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = path,
                                        UseShellExecute = false // Force consistency in behavior between Framework and Core
                                    };

                                    if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                    {
                                        startInfo.Arguments = metadata.ProcessArguments;
                                    }

                                    metadata.Process = Process.Start(startInfo);
                                    var timeout = 2000;

                                    while (timeout > 0)
                                    {
                                        // Loop and watch for evidence that the process is up and running
                                        if (metadata.ProcessIsHealthy())
                                        {
                                            metadata.SequentialFailures = 0;
                                            metadata.ProcessState = ProcessState.Healthy;
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
                    finally
                    {
                        Log.Warning("Keep alive is dropping for {Process}.", path);
                        metadata.IsBeingManaged = false;
                    }
                });
        }

        internal class ProcessMetadata
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

            public bool ProcessIsHealthy()
            {
                // Named pipe can return false in some circumstances while it is being written to, so have a fallback
                return NamedPipeIsBound() || ProgramIsRunning();
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

                    Log.Debug("Program [{Process}] is no longer running", ProcessPath);

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

            private bool NamedPipeIsBound()
            {
                return File.Exists($"\\\\.\\pipe\\{PipeName}");
            }
        }
    }
}
