// <copyright file="AgentProcessManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        private static readonly Lazy<IntPtr> JobObject = new Lazy<IntPtr>(JobObjectInterop.TryCreate);

        internal enum ProcessState
        {
            NeverChecked,
            ReadyToStart,
            Faulted,
            Healthy
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
                    Processes.Add(DogStatsDMetadata);
                }

                if (Processes.Count > 0)
                {
                    Log.Information(
                        "[aas-repro] init iis_worker_pid={Pid} app_domain={AppDomain} process_name={ProcessName} trace_pipe={TracePipe} stats_pipe={StatsPipe} trace_agent_path={TraceAgentPath}",
                        new object[]
                        {
#pragma warning disable CA1837
                            System.Diagnostics.Process.GetCurrentProcess().Id,
#pragma warning restore CA1837
                            DomainMetadata.Instance.AppDomainName,
                            DomainMetadata.Instance.ProcessName,
                            TraceAgentMetadata.PipeName ?? "<unset>",
                            DogStatsDMetadata.PipeName ?? "<unset>",
                            TraceAgentMetadata.ProcessPath ?? "<unset>"
                        });
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

                                    var pipeAtEntry = metadata.NamedPipeIsBoundDiagnostic();
                                    var pidsAtEntry = metadata.GetMatchingProcessesDiagnostic();
                                    Log.Information(
                                        "[aas-repro] never_checked process={Process} pipe_bound={PipeBound} matching_pids={Pids}",
                                        path,
                                        pipeAtEntry,
                                        pidsAtEntry);

                                    if (pipeAtEntry || pidsAtEntry.Length > 0)
                                    {
                                        // Assume healthy to start, but look for problems
                                        metadata.ProcessState = ProcessState.Healthy;

                                        var attempts = 7;
                                        var delay = 50d;
                                        var initialAttempts = attempts;

                                        while (--attempts > 0)
                                        {
                                            await Task.Delay((int)delay).ConfigureAwait(false);

                                            var pipeNow = metadata.NamedPipeIsBoundDiagnostic();
                                            var pidsNow = metadata.GetMatchingProcessesDiagnostic();
                                            Log.Information(
                                                "[aas-repro] never_checked_retry process={Process} attempt={Attempt} delay_ms={Delay} pipe_bound={PipeBound} matching_pids={Pids}",
                                                new object[] { path, initialAttempts - attempts, (int)delay, pipeNow, pidsNow });

                                            if (pipeNow || pidsNow.Length > 0)
                                            {
                                                delay = delay * 1.75d;
                                                continue;
                                            }

                                            Log.Information("[aas-repro] recovering process={Process} reason=previous_instance_gone", path);
                                            Log.Information("Recovering from previous {Process} shutdown. Ready for start.", path);
                                            metadata.ProcessState = ProcessState.ReadyToStart;
                                            break;
                                        }

                                        if (metadata.ProcessState == ProcessState.Healthy)
                                        {
                                            // BAD PATH: 7 retries exhausted, pipe-or-process still present.
                                            // No new datadog-trace-agent.exe will be started for this IIS Worker Process.
                                            Log.Warning(
                                                "[aas-repro] never_checked_exhausted process={Process} state=Healthy_no_new_agent pipe_bound={PipeBound} matching_pids={Pids}",
                                                path,
                                                metadata.NamedPipeIsBoundDiagnostic(),
                                                metadata.GetMatchingProcessesDiagnostic());
                                        }
                                    }
                                    else
                                    {
                                        Log.Information("[aas-repro] never_checked_clear process={Process} ready_to_start=true", path);
                                        metadata.ProcessState = ProcessState.ReadyToStart;
                                    }
                                }
                                else if (metadata.ProcessState == ProcessState.Healthy || metadata.ProcessState == ProcessState.Faulted)
                                {
                                    var pipeBound = metadata.NamedPipeIsBoundDiagnostic();
                                    var pids = metadata.GetMatchingProcessesDiagnostic();
                                    var healthyNow = pipeBound || pids.Length > 0;
                                    Log.Information(
                                        "[aas-repro] steady_poll process={Process} prev_state={Prev} pipe_bound={PipeBound} matching_pids={Pids} next_state={Next}",
                                        new object[] { path, metadata.ProcessState, pipeBound, pids, healthyNow ? "Healthy" : "ReadyToStart" });
                                    metadata.ProcessState = healthyNow ? ProcessState.Healthy : ProcessState.ReadyToStart;
                                }

                                if (metadata.ProcessState == ProcessState.ReadyToStart)
                                {
                                    Log.Information("[aas-repro] attempting_start process={Process}", path);
                                    Log.Information("Attempting to start {Process}.", path);

                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = path, UseShellExecute = false
                                    };

                                    if (!string.IsNullOrWhiteSpace(metadata.ProcessArguments))
                                    {
                                        startInfo.Arguments = metadata.ProcessArguments;
                                    }

                                    metadata.Process = Process.Start(startInfo);
                                    var spawnedPid = metadata.Process?.Id ?? -1;
                                    Log.Information("[aas-repro] spawned process={Process} child_pid={ChildPid}", new object[] { path, spawnedPid });

                                    if (metadata.Process is { } proc && !proc.HasExited)
                                    {
                                        JobObjectInterop.AssignAndLog(path, proc);
                                    }

                                    var timeout = 2000;

                                    while (timeout > 0)
                                    {
                                        if (metadata.ProcessIsHealthy())
                                        {
                                            metadata.SequentialFailures = 0;
                                            metadata.ProcessState = ProcessState.Healthy;
                                            Log.Information("[aas-repro] start_success process={Process} child_pid={ChildPid} after_ms={Elapsed}", new object[] { path, spawnedPid, 2000 - timeout });
                                            Log.Information("Successfully started {Process}.", path);
                                            break;
                                        }

                                        if (metadata.Process == null || metadata.Process.HasExited)
                                        {
                                            var exitCode = metadata.Process?.ExitCode;
                                            Log.Error(
                                                "[aas-repro] start_failed process={Process} child_pid={ChildPid} exit_code={ExitCode} after_ms={Elapsed}",
                                                new object[] { path, spawnedPid, exitCode?.ToString() ?? "<unknown>", 2000 - timeout });
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
                                    Log.Warning(
                                        "[aas-repro] sequential_failure process={Process} count={Count} max={Max}",
                                        new object[] { path, metadata.SequentialFailures, MaxFailures });
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

        internal sealed class ProcessMetadata
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

            // Always logs the pipe-check outcome (true or false), unlike NamedPipeIsBound which only logs false.
            internal bool NamedPipeIsBoundDiagnostic()
            {
                var namedPipe = $"\\\\.\\pipe\\{PipeName}";
                var bound = File.Exists(namedPipe);
                Log.Information("[aas-repro] pipe_check process={Process} pipe={Pipe} bound={Bound}", ProcessPath, namedPipe, bound);
                return bound;
            }

            // Returns a comma-separated "pid@HH:mm:ss.fff" string for all matching processes.
            // Returns empty string when no processes match.
            internal string GetMatchingProcessesDiagnostic()
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(ProcessPath))
                    {
                        return string.Empty;
                    }

                    var fileName = Path.GetFileNameWithoutExtension(ProcessPath);
                    var processesByName = Process.GetProcessesByName(fileName);

                    if (processesByName.Length == 0)
                    {
                        Log.Information("[aas-repro] process_check process={Process} name={Name} count=0", ProcessPath, fileName);
                        return string.Empty;
                    }

                    var sb = new StringBuilder();
                    for (var i = 0; i < processesByName.Length; i++)
                    {
                        if (i > 0) { sb.Append(','); }
                        try
                        {
                            sb.Append(processesByName[i].Id).Append('@').Append(processesByName[i].StartTime.ToString("HH:mm:ss.fff"));
                        }
                        catch
                        {
                            sb.Append(processesByName[i].Id).Append("@<unknown>");
                        }
                    }

                    var pidString = sb.ToString();
                    Log.Information("[aas-repro] process_check process={Process} name={Name} count={Count} pids={Pids}", new object[] { ProcessPath, fileName, processesByName.Length, pidString });
                    return pidString;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[aas-repro] process_check_error process={Process}", ProcessPath);
                    return string.Empty;
                }
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

            private bool NamedPipeIsBound()
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

        private static class JobObjectInterop
        {
            private const uint JobObjectLimitKillOnJobClose = 0x2000;
            private const int JobObjectExtendedLimitInformationClass = 9;

            [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            private static extern bool SetInformationJobObject(IntPtr hJob, int jobObjectInfoClass, ref JobObjectExtendedLimitInformation lpJobObjectInfo, int cbJobObjectInfoLength);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr hObject);

            public static IntPtr TryCreate()
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    return IntPtr.Zero;
                }

                try
                {
                    var handle = CreateJobObjectW(IntPtr.Zero, null);
                    if (handle == IntPtr.Zero)
                    {
                        var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        Log.Warning("[aas-repro] job_object_create_failed step=create win32_error={Err}", new object[] { err });
                        return IntPtr.Zero;
                    }

                    var info = default(JobObjectExtendedLimitInformation);
                    info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;

                    var size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation));
                    if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformationClass, ref info, size))
                    {
                        var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        Log.Warning("[aas-repro] job_object_create_failed step=set_info win32_error={Err}", new object[] { err });
                        CloseHandle(handle);
                        return IntPtr.Zero;
                    }

                    Log.Information("[aas-repro] job_object_create handle={Handle} kill_on_close=true", new object[] { handle.ToInt64().ToString("X") });
                    return handle;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[aas-repro] job_object_create_failed step=exception");
                    return IntPtr.Zero;
                }
            }

            public static void AssignAndLog(string path, Process process)
            {
                var pid = -1;
                try
                {
                    pid = process.Id;
                }
                catch
                {
                    // process may already be exiting
                }

                IntPtr jobHandle;
                try
                {
                    jobHandle = JobObject.Value;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[aas-repro] job_object_assign process={Process} child_pid={Pid} result=failed reason=lazy_init_threw", new object[] { path, pid });
                    return;
                }

                if (jobHandle == IntPtr.Zero)
                {
                    Log.Warning("[aas-repro] job_object_assign process={Process} child_pid={Pid} result=skipped reason=no_job_handle", new object[] { path, pid });
                    return;
                }

                IntPtr processHandle;
                try
                {
                    processHandle = process.Handle;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[aas-repro] job_object_assign process={Process} child_pid={Pid} result=failed reason=handle_unavailable", new object[] { path, pid });
                    return;
                }

                if (AssignProcessToJobObject(jobHandle, processHandle))
                {
                    Log.Information("[aas-repro] job_object_assign process={Process} child_pid={Pid} result=success", new object[] { path, pid });
                }
                else
                {
                    var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    Log.Warning("[aas-repro] job_object_assign process={Process} child_pid={Pid} result=failed win32_error={Err}", new object[] { path, pid, err });
                }
            }

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct IoCounters
            {
                public ulong ReadOperationCount;
                public ulong WriteOperationCount;
                public ulong OtherOperationCount;
                public ulong ReadTransferCount;
                public ulong WriteTransferCount;
                public ulong OtherTransferCount;
            }

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct JobObjectBasicLimitInformation
            {
                public long PerProcessUserTimeLimit;
                public long PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
            }

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct JobObjectExtendedLimitInformation
            {
                public JobObjectBasicLimitInformation BasicLimitInformation;
                public IoCounters IoInfo;
                public UIntPtr ProcessMemoryLimit;
                public UIntPtr JobMemoryLimit;
                public UIntPtr PeakProcessMemoryUsed;
                public UIntPtr PeakJobMemoryUsed;
            }
        }
    }
}
