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
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
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

        private static readonly ILogger Log = DatadogLogging.For<TracingProcessManager>();

        private static CancellationTokenSource _cancellationTokenSource;
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
                    Log.Error(ex, "Error when cancelling process {0}.", metadata.Name);
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

                if (DomainMetadata.ShouldAvoidAppDomain())
                {
                    Log.Information("Skipping process manager initialization for: {0}", DomainMetadata.AppDomainName);
                    return;
                }

                var traceAgentDirectory = Path.GetDirectoryName(TraceAgentMetadata.ProcessPath);

                if (!Directory.Exists(traceAgentDirectory))
                {
                    Log.Warning("Directory for trace agent does not exist: {0}", traceAgentDirectory);
                    return;
                }

                InitializePortManagerClaimFiles(traceAgentDirectory);
                _cancellationTokenSource = new CancellationTokenSource();

                if (_isProcessManager)
                {
                    Log.Debug("Starting sub-processes from process {0}, app domain {1}.", DomainMetadata.ProcessName, DomainMetadata.AppDomainName);
                    StartProcesses();
                }
                else
                {
                    Log.Debug("Initializing sub process port file watchers.");
                    foreach (var instance in Processes)
                    {
                        instance.InitializePortFileWatcher();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when attempting to initialize process manager.");
            }
        }

        private static void InitializePortManagerClaimFiles(string traceAgentDirectory)
        {
            var portManagerDirectory = Path.Combine(traceAgentDirectory, "port-manager");

            if (!Directory.Exists(portManagerDirectory))
            {
                Directory.CreateDirectory(portManagerDirectory);
            }

            var fileClaim = Path.Combine(portManagerDirectory, ClaimFileName());

            var portManagerFiles = Directory.GetFiles(portManagerDirectory);
            if (portManagerFiles.Length > 0)
            {
                var activePids = Process.GetProcesses().Select(p => p.Id.ToString()).ToList();
                foreach (var portManagerFileName in portManagerFiles)
                {
                    try
                    {
                        var claimPid = GetProcessIdFromFileName(portManagerFileName);
                        if (!activePids.Contains(claimPid))
                        {
                            File.Delete(portManagerFileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error when cleaning port claims.");
                    }
                }
            }

            var remainingFiles = Directory.GetFiles(portManagerDirectory);
            if (remainingFiles.Length == 0)
            {
                File.WriteAllText(fileClaim, DateTime.Now.ToString(CultureInfo.InvariantCulture));
                _isProcessManager = true;
            }
        }

        private static string ClaimFileName()
        {
            var fileClaimName = $"{DomainMetadata.ProcessId}_{DomainMetadata.AppDomainId}";
            return fileClaimName;
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
                    Log.Debug("There is no path configured for {0}.", metadata.Name);
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
                Log.Error(ex, "Failed to verify halt of the {0} process.", metadata.Name);
            }
        }

        private static bool ProgramIsRunning(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return false;
            }

            var processId = GetProcessIdFromFileName(fullPath);
            var processesByName = Process.GetProcessesByName(processId);

            if (processesByName?.Length > 0)
            {
                // We enforce a unique enough naming within contexts where we would use sub-processes
                return true;
            }

            return false;
        }

        private static string GetProcessIdFromFileName(string fullPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(fullPath);

            if (fileName == null)
            {
                return "-1";
            }

            var parts = fileName.Split('_');

            return parts[0];
        }

        private static Task StartProcessWithKeepAlive(ProcessMetadata metadata)
        {
            var path = metadata.ProcessPath;
            Log.Debug("Starting keep alive for {0}.", path);

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
                                Log.Debug("Shutdown triggered for keep alive {0}.", path);
                                return;
                            }

                            try
                            {
                                if (metadata.Process != null && metadata.Process.HasExited == false)
                                {
                                    Log.Debug("We already have an active reference to {0}.", path);
                                    continue;
                                }

                                if (ProgramIsRunning(path))
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
                                GrabFreePortForInstance(metadata);
                                metadata.Process = Process.Start(startInfo);

                                await Task.Delay(200);

                                if (metadata.Process == null || metadata.Process.HasExited)
                                {
                                    Log.Error("{0} has failed to start.", path);
                                    sequentialFailures++;
                                }
                                else
                                {
                                    Log.Debug("Successfully started {0}.", path);
                                    sequentialFailures = 0;
                                    metadata.AlertSubscribers();
                                    Log.Debug("Finished calling port subscribers for {0}.", metadata.Name);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Exception when trying to start an instance of {0}.", path);
                                sequentialFailures++;
                            }
                            finally
                            {
                                // Delay for a reasonable amount of time before we check to see if the process is alive again.
                                await Task.Delay(KeepAliveInterval);
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
                Log.Error(ex, "Error trying to get a free port.");
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
            Log.Debug("Attempting to use port {0} for the {1}.", instance.Port, instance.Name);

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
                    Log.Debug("Port file already exists.");
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
                Log.Debug("Port file has changed.");
                ReadPortAndAlertSubscribers();
            }

            private void OnPortFileDeleted(object source, FileSystemEventArgs e)
            {
                // For if some process or user decides to delete the port file, we have some evidence of what happened
                Log.Error("The port file ({0}) has been deleted.");
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
                            Log.Debug("Retrieved port {0} from {1}.", portValue, PortFilePath);
                            RefreshPortVars();
                        }
                        else
                        {
                            Log.Error("The port file ({0}) is malformed: {1}", PortFilePath, portText);
                        }

                        AlertSubscribers();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error when alerting subscribers for {0}", Name);
                        Thread.Sleep(5); // Wait just a tiny bit just to let the file come unlocked
                    }
                }
            }
        }
    }
}
