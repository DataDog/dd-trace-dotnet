using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    internal class TracingProcessManager
    {
        private static readonly List<ProcessMetadata> Processes = new List<ProcessMetadata>()
        {
            new ProcessMetadata()
            {
                Name = "datadog-trace-agent",
                ProcessPathKey = ConfigurationKeys.TraceAgentPath,
                ProcessArgumentsKey = ConfigurationKeys.TraceAgentArgs,
                PreStartAction = () =>
                {
                    var port = FreeTcpPort();
                    if (port == null)
                    {
                        throw new Exception("Unable to secure a port for trace agent");
                    }

                    var portString = port.ToString();
                    Api.OverrideTracePort(port.Value);
                    Environment.SetEnvironmentVariable(ConfigurationKeys.AgentPort, portString);
                    Environment.SetEnvironmentVariable(ConfigurationKeys.TraceAgentPortKey, portString);
                    DatadogLogging.RegisterStartupLog(log => log.Debug("Attempting to use port {0} for the trace agent.", portString));
                }
            },
            new ProcessMetadata()
            {
                Name = "dogstatsd",
                ProcessPathKey = ConfigurationKeys.DogStatsDPath,
                ProcessArgumentsKey = ConfigurationKeys.DogStatsDArgs,
                PreStartAction = () =>
                {
                    var port = FreeTcpPort();
                    if (port == null)
                    {
                        throw new Exception("Unable to secure a port for dogstatsd");
                    }

                    var portString = port.ToString();
                    StatsdUDP.OverridePort(port.Value);
                    Environment.SetEnvironmentVariable(StatsdConfig.DD_DOGSTATSD_PORT_ENV_VAR, portString);
                    DatadogLogging.RegisterStartupLog(log => log.Debug("Attempting to use port {0} for dogstatsd.", portString));
                }
            }
        };

        private static CancellationTokenSource _cancellationTokenSource;

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

        public static void StartProcesses()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                foreach (var subProcessMetadata in Processes)
                {
                    var processPath = Environment.GetEnvironmentVariable(subProcessMetadata.ProcessPathKey);

                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        var processArgs = Environment.GetEnvironmentVariable(subProcessMetadata.ProcessArgumentsKey);
                        subProcessMetadata.KeepAliveTask =
                            StartProcessWithKeepAlive(processPath, processArgs, subProcessMetadata);
                    }
                    else
                    {
                        DatadogLogging.RegisterStartupLog(log => log.Debug("There is no path configured for {0}.", subProcessMetadata.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Error when attempting to start standalone agent processes."));
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

        private static Task StartProcessWithKeepAlive(string path, string args, ProcessMetadata metadata)
        {
            DatadogLogging.RegisterStartupLog(log => log.Debug("Starting keep alive for {0}.", path));

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

                                if (!string.IsNullOrWhiteSpace(args))
                                {
                                    startInfo.Arguments = args;
                                }

                                DatadogLogging.RegisterStartupLog(log => log.Debug("Starting {0}.", path));
                                metadata.PreStartAction?.Invoke();
                                metadata.Process = Process.Start(startInfo);

                                Thread.Sleep(200);

                                if (metadata.Process == null || metadata.Process.HasExited)
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Error("{0} has failed to start.", path));
                                    sequentialFailures++;
                                }
                                else
                                {
                                    DatadogLogging.RegisterStartupLog(log => log.Debug("Successfully started {0}.", path));
                                    sequentialFailures = 0;
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
                        DatadogLogging.RegisterStartupLog(log => log.Debug("Keep alive is dropping for {0}.", path));
                    }
                });
        }

        private static int? FreeTcpPort()
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

        private class ProcessMetadata
        {
            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            public string ProcessPathKey { get; set; }

            public string ProcessArgumentsKey { get; set; }

            public Action PreStartAction { get; set; }
        }
    }
}
