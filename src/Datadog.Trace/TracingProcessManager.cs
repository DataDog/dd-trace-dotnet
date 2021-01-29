using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

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

        private static readonly ILogger Log = DatadogLogging.For<TracingProcessManager>();
        private static CancellationTokenSource _cancellationTokenSource;

        internal enum ProcessState
        {
            NotRunning,
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

                _cancellationTokenSource = new CancellationTokenSource();

                Log.Debug("Starting child processes from process {ProcessName}, AppDomain {AppDomain}.", DomainMetadata.ProcessName, DomainMetadata.AppDomainName);
                StartProcesses();
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Error when attempting to initialize process manager.");
            }
        }

        private static void StartProcesses()
        {
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

            return Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        metadata.IsBeingManaged = true;

                        while (true)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                Log.Debug("Shutdown triggered for keep alive {Process}.", path);
                                return;
                            }

                            if (metadata.SequentialFailures >= MaxFailures)
                            {
                                Log.Error("Circuit breaker triggered for {Process}. Max failed retries reached ({ErrorCount}).", path, MaxFailures);
                                metadata.ProcessState = ProcessState.Faulted;
                                return;
                            }

                            try
                            {
                                if (metadata.ProcessState == ProcessState.NotRunning)
                                {
                                    // This means we have never tried from this domain
                                    var pipeIsBound = metadata.NamedPipeIsBound();

                                    if (pipeIsBound)
                                    {
                                        // It is possible that if a pipe is bound it may still be shutting down from last time
                                        // Check on a delay to be sure we have the agent available
                                        var attempts = 7;
                                        var delay = 50d;

                                        while (--attempts > 0)
                                        {
                                            await Task.Delay((int)delay);
                                            if (!metadata.NamedPipeIsBound())
                                            {
                                                metadata.ProcessState = ProcessState.ReadyToStart;
                                                break;
                                            }

                                            // Should result in a max delay of ~3.28 seconds before giving up
                                            delay = delay * 1.75d;
                                        }

                                        metadata.ProcessState = ProcessState.Healthy;
                                    }
                                    else
                                    {
                                        // If no pipe is bound, kick it off
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                    }
                                }
                                else if (metadata.ProcessState == ProcessState.Healthy || metadata.ProcessState == ProcessState.Faulted)
                                {
                                    // This means we have tried to start from this domain before and we're in a keep alive check
                                    if (!metadata.NamedPipeIsBound())
                                    {
                                        // We must try to restart this process
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                    }
                                    else
                                    {
                                        // No need to try to start it
                                        metadata.ProcessState = ProcessState.Healthy;
                                    }

                                    Log.Debug("{Process} is already running.", path);
                                }

                                if (metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    var startInfo = new ProcessStartInfo { FileName = path };

                                    if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                    {
                                        startInfo.Arguments = metadata.ProcessArguments;
                                    }

                                    Log.Debug("Starting {Process}.", path);
                                    metadata.Process = Process.Start(startInfo);

                                    while (!metadata.NamedPipeIsBound())
                                    {
                                        await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);

                                        if (metadata.Process == null || metadata.Process.HasExited)
                                        {
                                            Log.Error("{Process} has failed to start.", path);
                                            metadata.SequentialFailures++;
                                            metadata.ProcessState = ProcessState.Faulted;
                                        }
                                    }

                                    Log.Debug("Successfully started {Process}.", path);
                                    metadata.SequentialFailures = 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                metadata.ProcessState = ProcessState.Faulted;
                                Log.Error(ex, "Exception when trying to start an instance of {Process}.", path);
                            }
                            finally
                            {
                                if (metadata.ProcessState == ProcessState.Faulted)
                                {
                                    metadata.SequentialFailures++;
                                    // Quicker retry in these cases
                                    await Task.Delay(ExceptionRetryInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                                }
                                else
                                {
                                    // Delay for a reasonable amount of time before we check to see if the process is alive again.
                                    await Task.Delay(KeepAliveInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Log.Debug("Keep alive is dropping for {Process}.", path);
                        metadata.IsBeingManaged = false;
                    }
                },
                TaskCreationOptions.LongRunning);
        }

        internal class ProcessMetadata
        {
            private string _processPath;
            private ProcessState _processState = ProcessState.NotRunning;

            public string PipeName { get; set; }

            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is being managed by active keep alive tasks.
            /// </summary>
            public bool IsBeingManaged { get; set; }

            public int SequentialFailures { get; set; }

            public ProcessState ProcessState
            {
                get => _processState;
                set
                {
                    PreviousState = _processState;
                    _processState = value;
                }
            }

            public ProcessState PreviousState { get; private set; }

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

            public bool ProgramIsRunning()
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

            public bool NamedPipeIsBound()
            {
                return File.Exists($"\\\\.\\pipe\\{PipeName}");
            }
        }
    }
}
