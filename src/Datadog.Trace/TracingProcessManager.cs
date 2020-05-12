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
    /// <summary>
    /// This class is used to manage agent processes in contexts where the user can not, such as Azure App Services.
    /// </summary>
    internal class TracingProcessManager
    {
        internal static readonly int KeepAliveInterval = 120_000;
        internal static readonly int ExceptionRetryInterval = 1_000;

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

        public static void TryForceTraceAgentRefresh()
        {
            if (string.IsNullOrEmpty(TraceAgentMetadata.DirectoryPath))
            {
                Log.Debug("This is not a context where we manage sub processes.");
                return;
            }

            Log.Warning("We are attempting to force a child process refresh.");
            InitializePortManagerClaimFiles(TraceAgentMetadata.DirectoryPath);

            if (!_isProcessManager)
            {
                Log.Debug("This process is not responsible for managing agent processes.");
                return;
            }

            if (Processes.All(p => p.HasAttemptedStartup))
            {
                Log.Debug("Forcing a full refresh on agent processes.");
                StopProcesses();

                _cancellationTokenSource = new CancellationTokenSource();

                Log.Debug("Starting child processes.");
                StartProcesses();
            }
            else
            {
                Log.Debug("This process has not had a chance to initialize agent processes.");
            }
        }

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
                    Log.Information("Skipping process manager initialization for AppDomain: {0}", DomainMetadata.AppDomainName);
                    return;
                }

                if (!Directory.Exists(TraceAgentMetadata.DirectoryPath))
                {
                    Log.Warning("Directory for trace agent does not exist: {0}", TraceAgentMetadata.DirectoryPath);
                    return;
                }

                InitializePortManagerClaimFiles(TraceAgentMetadata.DirectoryPath);
                _cancellationTokenSource = new CancellationTokenSource();

                if (_isProcessManager)
                {
                    Log.Debug("Starting child processes from process {0}, AppDomain {1}.", DomainMetadata.ProcessName, DomainMetadata.AppDomainName);
                    StartProcesses();
                }

                Log.Debug("Initializing sub process port file watchers.");
                foreach (var instance in Processes)
                {
                    instance.InitializePortFileWatcher();
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

            var fileClaim =
                Path.Combine(
                    portManagerDirectory,
                    string.Format(CultureInfo.InvariantCulture, "{0}_{1}", DomainMetadata.ProcessId, DomainMetadata.AppDomainId);

            var portManagerFiles = Directory.GetFiles(portManagerDirectory);
            if (portManagerFiles.Length > 0)
            {
                int? GetProcessIdFromFileName(string fullPath)
                {
                    var fileName = Path.GetFileNameWithoutExtension(fullPath);
                    if (int.TryParse(NumberStyles.Integer, CultureInfo.InvariantCulture, fileName?.Split('_')[0], out var pid))
                    {
                        return pid;
                    }

                    return null;
                }

                foreach (var portManagerFileName in portManagerFiles)
                {
                    try
                    {
                        var claimPid = GetProcessIdFromFileName(portManagerFileName);
                        var isActive = false;

                        try
                        {
                            if (claimPid != null)
                            {
                                using (var process = Process.GetProcessById(claimPid.Value))
                                {
                                    isActive = true;
                                }
                            }
                        }
                        finally
                        {
                            if (!isActive)
                            {
                                File.Delete(portManagerFileName);
                            }
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
                File.WriteAllText(fileClaim, DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
                _isProcessManager = true;
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
                    Log.Debug("There is no path configured for {0}.", metadata.Name);
                }
            }
        }

        private static void SafelyKillProcess(ProcessMetadata metadata)
        {
            try
            {
                if (_isProcessManager)
                {
                    metadata.SafelyForceKill();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to verify halt of the {0} process.", metadata.Name);
            }
        }

        private static Task StartProcessWithKeepAlive(ProcessMetadata metadata)
        {
            const int circuitBreakerMax = 3;
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
                                GrabFreePortForInstance(metadata);
                                metadata.Process = Process.Start(startInfo);

                                await Task.Delay(150, _cancellationTokenSource.Token);

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
                                    await Task.Delay(ExceptionRetryInterval, _cancellationTokenSource.Token);
                                }
                                else
                                {
                                    await Task.Delay(KeepAliveInterval, _cancellationTokenSource.Token);
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

            public string PortFilePath { get; private set; }

            public string ProcessPath
            {
                get => _processPath;
                set
                {
                    _processPath = value;
                    DirectoryPath = Path.GetDirectoryName(_processPath);
                    PortFilePath = !string.IsNullOrWhiteSpace(_processPath) ? $"{_processPath}.port" : null;
                }
            }

            public string DirectoryPath { get; private set; }

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
                try
                {
                    _portFileWatcher?.Dispose();
                    Process?.Dispose();
                    KeepAliveTask?.Dispose();

                    if (_isProcessManager)
                    {
                        // Do our best to give new instances a fresh slate
                        File.Delete(PortFilePath);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Error when disposing of process manager resources.");
                    }
                    catch
                    {
                        // ignore for dispose, to be safe
                    }
                }
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

            public void SafelyForceKill()
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(ProcessPath);
                    var processesByName = Process.GetProcessesByName(fileName);
                    foreach (var process in processesByName)
                    {
                        try
                        {
                            process.Kill();
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Error when force killing process {0}.", ProcessPath);
                    }
                    catch
                    {
                        // ignore, to be safe
                    }
                }
            }

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

                    if (processesByName?.Length > 0)
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
