using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    internal class TracingProcessManager
    {
        private static readonly HashSet<string> AzureTopLevelProcesses = new HashSet<string>() { "w3wp.exe" };

        private static readonly ProcessMetadata TraceAgentMetadata = new ProcessMetadata
        {
            Name = "datadog-trace-agent",
            ProcessPath = Environment.GetEnvironmentVariable(ConfigurationKeys.TraceAgentPath),
            ProcessArguments = Environment.GetEnvironmentVariable(ConfigurationKeys.TraceAgentArgs),
            RefreshPortVars = () =>
            {
                var portString = TraceAgentMetadata.Port?.ToString();
                Environment.SetEnvironmentVariable(ConfigurationKeys.AgentPort, portString);
                Environment.SetEnvironmentVariable(ConfigurationKeys.TraceAgentPortKey, portString);
            }
        };

        private static readonly ProcessMetadata DogStatsDMetadata = new ProcessMetadata
        {
            Name = "dogstatsd",
            ProcessPath = Environment.GetEnvironmentVariable(ConfigurationKeys.DogStatsDPath),
            ProcessArguments = Environment.GetEnvironmentVariable(ConfigurationKeys.DogStatsDArgs),
            RefreshPortVars = () =>
            {
                var portString = DogStatsDMetadata.Port?.ToString();
                Environment.SetEnvironmentVariable(StatsdConfig.DD_DOGSTATSD_PORT_ENV_VAR, portString);
            }
        };

        private static readonly List<ProcessMetadata> Processes = new List<ProcessMetadata>()
        {
            TraceAgentMetadata,
            DogStatsDMetadata
        };

        private static CancellationTokenSource _cancellationTokenSource;

        public static void SubscribeToTraceAgentPortOverride(Action<int> subscriber)
        {
            TraceAgentMetadata.PortSubscribers.Add(subscriber);

            if (TraceAgentMetadata.Port != null)
            {
                subscriber(TraceAgentMetadata.Port.Value);
            }
        }

        public static void SubscribeToDogStatsDPortOverride(Action<int> subscriber)
        {
            DogStatsDMetadata.PortSubscribers.Add(subscriber);

            if (DogStatsDMetadata.Port != null)
            {
                subscriber(DogStatsDMetadata.Port.Value);
            }
        }

        public static void StopProcesses()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                foreach (var subProcessMetadata in Processes)
                {
                    SafelyKillProcess(subProcessMetadata);
                }
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when cancelling processes."));
            }
        }

        public static void Initialize()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var currentProcess = Process.GetCurrentProcess();
                if (AzureTopLevelProcesses.Contains(currentProcess.ProcessName.ToLowerInvariant()))
                {
                    StartProcesses();
                }
                else
                {
                    foreach (var instance in Processes)
                    {
                        instance.InitializePortFileWatcher();
                    }
                }
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when attempting to start standalone agent processes."));
            }
        }

        private static void StartProcesses()
        {
            foreach (var metadata in Processes)
            {
                if (!string.IsNullOrWhiteSpace(metadata.ProcessPath))
                {
                    metadata.KeepAliveTask = StartProcessWithKeepAlive(metadata);
                }
                else
                {
                    DatadogLogging.RegisterStartupLog(log => log.Debug("There is no path configured for {0}.", metadata.Name));
                }
            }
        }

        private static void SafelyKillProcess(ProcessMetadata metadata)
        {
            try
            {
                metadata.Dispose();

                if (metadata.Process != null && !metadata.Process.HasExited)
                {
                    metadata.Process.Kill();
                }
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Failed to verify halt of the {0} process.", metadata.Name));
            }
        }

        private static bool ProgramIsRunning(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return false;
            }

            var fileName = Path.GetFileNameWithoutExtension(fullPath);
            var processesByName = Process.GetProcessesByName(fileName);

            if (processesByName?.Length > 0)
            {
                // We enforce a unique enough naming within contexts where we would use sub-processes
                return true;
            }

            return false;
        }

        private static Task StartProcessWithKeepAlive(ProcessMetadata metadata)
        {
            DatadogLogging.RegisterStartupLog(log => log.Debug("Starting keep alive for {0}.", metadata.ProcessPath));

            return Task.Run(
                () =>
                {
                    try
                    {
                        var circuitBreakerMax = 3;
                        var sequentialFailures = 0;

                        while (true)
                        {
                            if (_cancellationTokenSource.IsCancellationRequested)
                            {
                                DatadogLogging.RegisterStartupLog(log => log.Debug("Shutdown triggered for keep alive {0}.", metadata.ProcessPath));
                                return;
                            }

                            try
                            {
                                if (metadata.Process != null && metadata.Process.HasExited == false)
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("We already have an active reference to {0}.", metadata.ProcessPath));
                                    continue;
                                }

                                if (ProgramIsRunning(metadata.ProcessPath))
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("{0} is already running.", metadata.ProcessPath));
                                    continue;
                                }

                                var startInfo = new ProcessStartInfo { FileName = metadata.ProcessPath };

                                if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                {
                                    startInfo.Arguments = metadata.ProcessArguments;
                                }

                                DatadogLogging.RegisterStartupLog(log => log.Debug("Starting {0}.", metadata.ProcessPath));
                                GrabFreePortForInstance(metadata);
                                metadata.Process = Process.Start(startInfo);

                                Thread.Sleep(200);

                                if (metadata.Process == null || metadata.Process.HasExited)
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Error("{0} has failed to start.", metadata.ProcessPath));
                                    sequentialFailures++;
                                }
                                else
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("Successfully started {0}.", metadata.ProcessPath));
                                    sequentialFailures = 0;
                                    metadata.AlertSubscribers();
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("Finished calling port subscribers for {0}.", metadata.Name));
                                }
                            }
                            catch (Exception ex)
                            {
                                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Exception when trying to start an instance of {0}.", metadata.ProcessPath));
                                sequentialFailures++;
                            }
                            finally
                            {
                                // Delay for a reasonable amount of time before we check to see if the process is alive again.
                                Thread.Sleep(20_000);
                            }

                            if (sequentialFailures >= circuitBreakerMax)
                            {
                                DatadogLogging.RegisterStartupLog(log => log.Error("Circuit breaker triggered for {0}. Max failed retries reached ({1}).", path, sequentialFailures));
                                break;
                            }
                        }
                    }
                    finally
                    {
                        DatadogLogging.RegisterStartupLog(log => log.Debug("Keep alive is dropping for {0}.", metadata.ProcessPath));
                    }
                });
        }

        private static int? GetFreeTcpPort()
        {
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, 0);
                tcpListener.Start();
                var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                return port;
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error trying to get a free port."));
                return null;
            }
            finally
            {
                tcpListener?.Stop();
            }
        }

        private static void GrabFreePortForInstance(ProcessMetadata instance)
        {
            instance.Port = GetFreeTcpPort();
            if (instance.Port == null)
            {
                throw new Exception($"Unable to secure a port for {instance.Name}");
            }

            instance.RefreshPortVars();
            DatadogLogging.RegisterStartupLog(log => log.Debug("Attempting to use port {0} for the {1}.", instance.Port, instance.Name));

            if (instance.PortFilePath != null)
            {
                File.WriteAllText(instance.PortFilePath, instance.Port.Value.ToString());
            }
        }

        private class ProcessMetadata : IDisposable
        {
            private string _processPath;
            private FileSystemWatcher _portFileWatcher;

            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            public string PortFilePath { get; private set; }

            public string ProcessPath
            {
                get => _processPath;
                set
                {
                    _processPath = value;
                    PortFilePath = !string.IsNullOrWhiteSpace(_processPath) ? $"{_processPath}-port" : null;
                }
            }

            public string ProcessArguments { get; set; }

            public Action RefreshPortVars { get; set; }

            public int? Port { get; set; }

            public ConcurrentBag<Action<int>> PortSubscribers { get; } = new ConcurrentBag<Action<int>>();

            public void AlertSubscribers()
            {
                if (Port != null)
                {
                    foreach (var portSubscriber in PortSubscribers)
                    {
                        portSubscriber(Port.Value);
                    }
                }
            }

            public void Dispose()
            {
                _portFileWatcher?.Dispose();
                Process?.Dispose();
                KeepAliveTask?.Dispose();
            }

            public void InitializePortFileWatcher()
            {
                _portFileWatcher = new FileSystemWatcher
                {
                    NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite,
                    Path = Path.GetDirectoryName(PortFilePath),
                    Filter = Path.GetFileName(PortFilePath)
                };

                _portFileWatcher.Created += OnPortFileChanged;
                _portFileWatcher.Changed += OnPortFileChanged;
                _portFileWatcher.EnableRaisingEvents = true;
            }

            private void OnPortFileChanged(object source, FileSystemEventArgs e)
            {
                var portFile = PortFilePath;
                var portText = File.ReadAllText(portFile);
                if (int.TryParse(portText, out var portValue))
                {
                    Port = portValue;
                    RefreshPortVars();
                }

                AlertSubscribers();
            }
        }
    }
}
