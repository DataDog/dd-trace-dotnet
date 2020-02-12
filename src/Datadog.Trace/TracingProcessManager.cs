using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

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
            },
            new ProcessMetadata()
            {
                Name = "dogstatsd",
                ProcessPathKey = ConfigurationKeys.DogStatsDPath,
                ProcessArgumentsKey = ConfigurationKeys.DogStatsDArgs,
            }
        };

        public static void StopSubProcesses()
        {
            foreach (var subProcessMetadata in Processes)
            {
                SafelyKillProcess(subProcessMetadata);
            }
        }

        public static void StartProcesses()
        {
            try
            {
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

                metadata.KeepAliveTask?.Dispose();
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

        private class ProcessMetadata
        {
            public string Name { get; set; }

            public Process Process { get; set; }

            public Task KeepAliveTask { get; set; }

            public string ProcessPathKey { get; set; }

            public string ProcessArgumentsKey { get; set; }
        }
    }
}
