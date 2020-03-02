using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
        internal static readonly int KeepAliveInterval = 20_000;

        internal static readonly ProcessMetadata TraceAgentMetadata = new ProcessMetadata
        {
            Name = "datadog-trace-agent",
            ProcessPath = Environment.GetEnvironmentVariable(ConfigurationKeys.TraceAgentPath),
            ProcessArguments = Environment.GetEnvironmentVariable(ConfigurationKeys.TraceAgentArgs),
            RefreshPortVars = () =>
            {
                var portString = TraceAgentMetadata.Port?.ToString(CultureInfo.InvariantCulture);
                Environment.SetEnvironmentVariable(ConfigurationKeys.AgentPort, portString);
                Environment.SetEnvironmentVariable(ConfigurationKeys.TraceAgentPortKey, portString);
            }
        };

        internal static readonly ProcessMetadata DogStatsDMetadata = new ProcessMetadata
        {
            Name = "dogstatsd",
            ProcessPath = Environment.GetEnvironmentVariable(ConfigurationKeys.DogStatsDPath),
            ProcessArguments = Environment.GetEnvironmentVariable(ConfigurationKeys.DogStatsDArgs),
            RefreshPortVars = () =>
            {
                var portString = DogStatsDMetadata.Port?.ToString(CultureInfo.InvariantCulture);
                Environment.SetEnvironmentVariable(StatsdConfig.DD_DOGSTATSD_PORT_ENV_VAR, portString);
            }
        };

        private static readonly List<ProcessMetadata> Processes = new List<ProcessMetadata>()
        {
            TraceAgentMetadata,
            DogStatsDMetadata
        };

        private static CancellationTokenSource _cancellationTokenSource;
        private static string _processName;
        private static int _processId;
        private static bool _isProcessManager;

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
            _cancellationTokenSource?.Cancel();

            foreach (var metadata in Processes)
            {
                try
                {
                    SafelyKillProcess(metadata);
                    metadata.Dispose();
                }
                catch (Exception ex)
                {
                    DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when cancelling process {0}.", metadata.Name));
                }
            }
        }

        public static void Initialize()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TraceAgentMetadata.ProcessPath))
                {
                    return;
                }

                var traceAgentDirectory = Path.GetDirectoryName(TraceAgentMetadata.ProcessPath);

                if (!Directory.Exists(traceAgentDirectory))
                {
                    DatadogLogging.RegisterStartupLog(log => log.Debug("Directory for trace agent does not exist: {0}", traceAgentDirectory));
                    return;
                }

                InitializePortManagerClaimFiles(traceAgentDirectory);
                _cancellationTokenSource = new CancellationTokenSource();

                if (_isProcessManager)
                {
                    var currentAppDomainName = AppDomain.CurrentDomain.FriendlyName.ToLowerInvariant();
                    DatadogLogging.RegisterStartupLog(log => log.Debug("Starting sub-processes from top level process {0}, app domain {1}.", _processName, currentAppDomainName));
                    StartProcesses();
                }
                else
                {
                    DatadogLogging.RegisterStartupLog(log => log.Debug("Initializing sub process port file watchers."));
                    foreach (var instance in Processes)
                    {
                        instance.InitializePortFileWatcher();
                    }
                }
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when attempting to initialize process manager."));
            }
        }

        private static void InitializePortManagerClaimFiles(string traceAgentDirectory)
        {
            var portManagerDirectory = Path.Combine(traceAgentDirectory, "port-manager");

            if (!Directory.Exists(portManagerDirectory))
            {
                Directory.CreateDirectory(portManagerDirectory);
            }

            var currentProcess = Process.GetCurrentProcess();
            _processName = currentProcess.ProcessName.ToLowerInvariant();
            _processId = currentProcess.Id;
            var fileClaim = Path.Combine(portManagerDirectory, _processId.ToString());

            var portManagerFiles = Directory.GetFiles(portManagerDirectory);
            var deleted = 0;
            if (portManagerFiles.Length > 0)
            {
                var activePids = Process.GetProcesses().Select(p => p.Id.ToString()).ToList();
                foreach (var portManagerFileName in portManagerFiles)
                {
                    try
                    {
                        var claimPid = Path.GetFileName(portManagerFileName);
                        if (!activePids.Contains(claimPid))
                        {
                            File.Delete(portManagerFileName);
                            deleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when cleaning port claims."));
                    }
                }
            }

            if (deleted == portManagerFiles.Length)
            {
                File.WriteAllText(fileClaim, DateTime.Now.ToString(CultureInfo.InvariantCulture));
                _isProcessManager = true;
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
            var path = metadata.ProcessPath;
            DatadogLogging.RegisterStartupLog(log => log.Debug("Starting keep alive for {0}.", path));

            return Task.Run(
                async () =>
                {
                    try
                    {
                        var circuitBreakerMax = 3;
                        var sequentialFailures = 0;

                        while (true)
                        {
                            if (_cancellationTokenSource.IsCancellationRequested)
                            {
                                DatadogLogging.RegisterStartupLog(log => log.Debug("Shutdown triggered for keep alive {0}.", path));
                                return;
                            }

                            try
                            {
                                if (metadata.Process != null && metadata.Process.HasExited == false)
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("We already have an active reference to {0}.", path));
                                    continue;
                                }

                                if (ProgramIsRunning(path))
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("{0} is already running.", path));
                                    continue;
                                }

                                var startInfo = new ProcessStartInfo { FileName = path };

                                if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                {
                                    startInfo.Arguments = metadata.ProcessArguments;
                                }

                                DatadogLogging.RegisterStartupLog(log => log.Debug("Starting {0}.", path));
                                GrabFreePortForInstance(metadata);
                                metadata.Process = Process.Start(startInfo);

                                await Task.Delay(200);

                                if (metadata.Process == null || metadata.Process.HasExited)
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Error("{0} has failed to start.", path));
                                    sequentialFailures++;
                                }
                                else
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("Successfully started {0}.", path));
                                    sequentialFailures = 0;
                                    metadata.AlertSubscribers();
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("Finished calling port subscribers for {0}.", metadata.Name));
                                }
                            }
                            catch (Exception ex)
                            {
                                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Exception when trying to start an instance of {0}.", path));
                                sequentialFailures++;
                            }
                            finally
                            {
                                // Delay for a reasonable amount of time before we check to see if the process is alive again.
                                await Task.Delay(KeepAliveInterval);
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
                        DatadogLogging.RegisterStartupLog(log => log.Debug("Keep alive is dropping for {0}.", path));
                    }
                });
        }

        private static string ReadSingleLineNoLock(string file)
        {
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs))
                {
                    return sr.ReadLine();
                }
            }
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
                File.WriteAllText(instance.PortFilePath, instance.Port.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        internal class ProcessMetadata : IDisposable
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
                    PortFilePath = !string.IsNullOrWhiteSpace(_processPath) ? $"{_processPath}.port" : null;
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
                if (File.Exists(PortFilePath))
                {
                    DatadogLogging.RegisterStartupLog(log => log.Debug("Port file already exists."));
                    ReadPortAndAlertSubscribers();
                }

                _portFileWatcher = new FileSystemWatcher
                {
                    NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.Size,
                    Path = Path.GetDirectoryName(PortFilePath),
                    Filter = Path.GetFileName(PortFilePath)
                };

                _portFileWatcher.Created += OnPortFileChanged;
                _portFileWatcher.Changed += OnPortFileChanged;
                _portFileWatcher.Deleted += OnPortFileDeleted;
                _portFileWatcher.EnableRaisingEvents = true;
            }

            public void ForcePortFileRead()
            {
                if (KeepAliveTask == null)
                {
                    // There is nothing to accomplish, ports are not dynamic
                    return;
                }

                ReadPortAndAlertSubscribers();
            }

            private void OnPortFileChanged(object source, FileSystemEventArgs e)
            {
                DatadogLogging.RegisterStartupLog(log => log.Debug("Port file has changed."));
                ReadPortAndAlertSubscribers();
            }

            private void OnPortFileDeleted(object source, FileSystemEventArgs e)
            {
                // For if some process or user decides to delete the port file, we have some evidence of what happened
                DatadogLogging.RegisterStartupLog(log => log.Error("The port file ({0}) has been deleted."));
            }

            private void ReadPortAndAlertSubscribers()
            {
                var retries = 3;

                while (retries-- > 0)
                {
                    try
                    {
                        var portFile = PortFilePath;
                        var portText = ReadSingleLineNoLock(portFile);
                        if (int.TryParse(portText, NumberStyles.Any, CultureInfo.InvariantCulture, out var portValue))
                        {
                            if (Port == portValue)
                            {
                                // nothing to do, let's not cause churn
                                return;
                            }

                            Port = portValue;
                            DatadogLogging.RegisterStartupLog(log => log.Debug("Retrieved port {0} from {1}.", portValue, PortFilePath));
                            RefreshPortVars();
                        }
                        else
                        {
                            DatadogLogging.RegisterStartupLog(log => log.Error("The port file ({0}) is malformed: {1}", PortFilePath, portText));
                        }

                        AlertSubscribers();
                    }
                    catch (Exception ex)
                    {
                        DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when alerting subscribers for {0}", Name));
                        Thread.Sleep(5); // Wait just a tiny bit just to let the file come unlocked
                    }
                }
            }
        }
    }
}
