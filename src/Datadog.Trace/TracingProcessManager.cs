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
        internal static readonly int ExceptionRetryInterval = 1_000;

        internal static readonly ProcessMetadata TraceAgentMetadata = new ProcessMetadata
        {
            Name = "datadog-trace-agent",
            ProcessPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.TraceAgentPath),
            ProcessArguments = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.TraceAgentArgs),
        };

        internal static readonly ProcessMetadata DogStatsDMetadata = new ProcessMetadata
        {
            Name = "dogstatsd",
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
                    Log.Information("Skipping process manager initialization for AppDomain: {0}", DomainMetadata.AppDomainName);
                    return;
                }

                if (!Directory.Exists(TraceAgentMetadata.DirectoryPath))
                {
                    Log.Warning("Directory for trace agent does not exist: {0}", TraceAgentMetadata.DirectoryPath);
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();

                Log.Debug("Starting child processes from process {0}, AppDomain {1}.", DomainMetadata.ProcessName, DomainMetadata.AppDomainName);
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
                        metadata.HasAttemptedStartup = false;
                        metadata.KeepAliveTask = StartProcessWithKeepAlive(metadata);
                    }
                }
                else
                {
                    Log.Debug("There is no path configured for {0}.", metadata.Name);
                }
            }
        }

        private static Task StartProcessWithKeepAlive(ProcessMetadata metadata)
        {
            const int circuitBreakerMax = 5;
            var path = metadata.ProcessPath;
            Log.Debug("Starting keep alive for {0}.", path);

            return Task.Run(
                async () =>
                {
                    try
                    {
                        metadata.IsBeingManaged = true;
                        var sequentialFailures = 0;

                        while (true)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                Log.Debug("Shutdown triggered for keep alive {0}.", path);
                                return;
                            }

                            try
                            {
                                metadata.IsFaulted = false;

                                if (metadata.ProgramIsRunning())
                                {
                                    Log.Debug("{0} is already running.", path);
                                    continue;
                                }

                                var startInfo = new ProcessStartInfo { FileName = path };

                                if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                {
                                    startInfo.Arguments = metadata.ProcessArguments;
                                }

                                Log.Debug("Starting {0}.", path);
                                metadata.Process = Process.Start(startInfo);

                                await Task.Delay(150, _cancellationTokenSource.Token).ConfigureAwait(false);

                                if (metadata.Process == null || metadata.Process.HasExited)
                                {
                                    Log.Error("{0} has failed to start.", path);
                                    sequentialFailures++;
                                }
                                else
                                {
                                    Log.Debug("Successfully started {0}.", path);
                                    sequentialFailures = 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                metadata.IsFaulted = true;
                                Log.Error(ex, "Exception when trying to start an instance of {0}.", path);
                                sequentialFailures++;
                            }
                            finally
                            {
                                metadata.HasAttemptedStartup = true;
                                // Delay for a reasonable amount of time before we check to see if the process is alive again.
                                if (metadata.IsFaulted)
                                {
                                    // Quicker retry in these cases
                                    await Task.Delay(ExceptionRetryInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                                }
                                else
                                {
                                    await Task.Delay(KeepAliveInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                                }
                            }

                            if (sequentialFailures >= circuitBreakerMax)
                            {
                                Log.Error("Circuit breaker triggered for {0}. Max failed retries reached ({1}).", path, sequentialFailures);
                                break;
                            }
                        }
                    }
                    finally
                    {
                        Log.Debug("Keep alive is dropping for {0}.", path);
                        metadata.IsBeingManaged = false;
                    }
                },
                _cancellationTokenSource.Token);
        }

        internal class ProcessMetadata
        {
            private string _processPath;

            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is being managed by active keep alive tasks.
            /// </summary>
            public bool IsBeingManaged { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the last attempt at running this process faulted.
            /// </summary>
            public bool IsFaulted { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the process has ever been tried.
            /// </summary>
            public bool HasAttemptedStartup { get; set; }

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

                    Log.Debug("Program [{0}] is no longer running", ProcessPath);

                    return false;
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Error when checking for running program {0}.", ProcessPath);
                    }
                    catch
                    {
                        // ignore, to be safe
                    }

                    return false;
                }
            }
        }
    }
}
