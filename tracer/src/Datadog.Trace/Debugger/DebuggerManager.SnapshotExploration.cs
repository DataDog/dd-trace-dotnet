// <copyright file="DebuggerManager.SnapshotExploration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.Debugger
{
    /// <summary>
    /// Snapshot-exploration-test specific initialization for <see cref="DebuggerManager"/>.
    /// Kept as a partial class so the regular debugger code path stays free of test plumbing.
    /// All code in this file only runs when <c>DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_ROOT_PATH</c> is set
    /// and the current process is a vstest test host.
    /// </summary>
    internal sealed partial class DebuggerManager
    {
        private static void WaitForProbeInstallation(TimeSpan timeout)
        {
            // No managed signal exists for "native received probes"; the only available
            // indicator is the native log file which is written from a different process.
            // Polling at NativeLogPollInterval is the simplest reliable approach here.
            var logDir = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.LogDirectory)
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Datadog .NET Tracer", "logs");

            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (NativeLogContainsProbeReceivedMarker(logDir))
                    {
                        // Native marker is logged before ReJIT requests are fully queued.
                        // Give native a small grace window so the queue is in place by the
                        // time tests start hitting probes.
                        Thread.Sleep(SnapshotExplorationConstants.NativeReadyGracePeriod);
                        Log.Information("Native profiler received probes, proceeding with tests.");
                        return;
                    }
                }
                catch (IOException)
                {
                    // Log file is being written by native and may be temporarily locked.
                }

                Thread.Sleep(SnapshotExplorationConstants.NativeLogPollInterval);
            }

            Log.Warning("Timeout waiting for native to receive probes. Proceeding anyway.");
        }

        private static bool NativeLogContainsProbeReceivedMarker(string logDir)
        {
            foreach (var logFile in Directory.GetFiles(logDir, SnapshotExplorationConstants.NativeLogFilePattern))
            {
                using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains(SnapshotExplorationConstants.NativeProbesReceivedMarker)
                     && line.Contains(SnapshotExplorationConstants.NativeProbesReceivedSuffix))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void InitForSnapshotExploration()
        {
            var tracerManager = TracerManager.Instance;
            var di = DebuggerFactory.CreateDynamicInstrumentation(NullDiscoveryService.Instance, RcmSubscriptionManager.Instance, tracerManager.Settings, ServiceNameProvider, DebuggerSettings, tracerManager.GitMetadataTagsProvider);

            Log.Information("Initializing Dynamic Instrumentation for snapshot exploration test.");
            EnsureSnapshotPipelineConfigured(DebuggerSettings);
            di.Initialize();
            _dynamicInstrumentation = di;

            // We must block until DI initialization completes before tests start, otherwise
            // DynamicInstrumentation.IsInitialized is still false and AddSnapshot becomes a
            // no-op (the null-conditional operator on DynamicInstrumentation skips it).
            // Awaiting the task is preferred over polling IsInitialized.
            di.GetInitializationTask().Wait(SnapshotExplorationConstants.InitializationTimeout);
            if (di.IsInitialized)
            {
                Log.Information("DynamicInstrumentation initialized successfully.");
                WaitForProbeInstallation(SnapshotExplorationConstants.ProbeInstallationTimeout);
            }
            else
            {
                Log.Warning("Timeout waiting for DynamicInstrumentation to initialize. Snapshots may not be captured.");
            }
        }
    }
}
