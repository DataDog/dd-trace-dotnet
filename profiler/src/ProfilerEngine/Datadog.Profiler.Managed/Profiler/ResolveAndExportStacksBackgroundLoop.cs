// <copyright file="ResolveAndExportStacksBackgroundLoop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Configuration;
using Datadog.PProf.Export;
using Datadog.Util;

namespace Datadog.Profiler
{
    internal class ResolveAndExportStacksBackgroundLoop : BackgroundLoopBase
    {
        private const string LogComponentMoniker = "Profiler-" + nameof(ResolveAndExportStacksBackgroundLoop);
        private const CompressionLevel PProfHttpExportGZipCompressionLevel = CompressionLevel.Optimal;

        private readonly ProfilerEngine _profilerEngine;
        private readonly TimeSpan _exportInterval;
        private readonly ulong _totalByteCountThreshold;
        private readonly ulong _totalSnapshotsThreshold;

        private readonly EfficientHttpClient _httpClient;
        private readonly string _profilesIngestionEndpoint_url;
        private readonly string _profilesIngestionEndpoint_apiKey;

        private readonly string _ddDataTags_Host;
        private readonly string _ddDataTags_Service;
        private readonly string _ddDataTags_Env;
        private readonly string _ddDataTags_Version;
        private readonly IReadOnlyList<KeyValuePair<string, string>> _ddDataTags_CustomTags;

        private readonly LocalFilesProfilesExporter _localFilesProfilesExporter;
        private readonly MetricsSender _metricsSender;
        private readonly PProfBuilder.TryResolveLocationSymbolsDelegate _tryResolveStackFrameSymbolsDelegate;
        private readonly bool _frameKinds_Native_IsEnabled;

        private long _totalPProfSuccessExportsCount = 0;
        private ExportStatistics _exportStatistics;

        // To protect against extreme/unreasonable configuration settings:

        public ResolveAndExportStacksBackgroundLoop(
                    ProfilerEngine profilerEngine,
                    IProductConfiguration config)
            : base(LogComponentMoniker)
        {
            Validate.NotNull(profilerEngine, nameof(profilerEngine));
            Validate.NotNull(config, nameof(config));

            _profilerEngine = profilerEngine;

            _exportInterval =
                ConfigGuard.ApplyInRange(
                    config.ProfilesExport_DefaultInterval,
                    ConfigGuard.ExportIntervalMin,
                    ConfigGuard.ExportIntervalMax);

            _totalByteCountThreshold =
                ConfigGuard.ApplyInRange(
                    (ulong)config.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsBytes,
                    ConfigGuard.BytesMin,
                    ConfigGuard.BytesMax);

            _totalSnapshotsThreshold =
                ConfigGuard.ApplyInRange(
                    (ulong)config.ProfilesExport_EarlyTriggerOnCollectedStackSnapshotsCount,
                    ConfigGuard.CountMin,
                    ConfigGuard.CountMax);

            _httpClient = new EfficientHttpClient();
            _profilesIngestionEndpoint_url = ConstructProfilesIngestionEndpointUrl(config);
            _profilesIngestionEndpoint_apiKey =
                string.IsNullOrWhiteSpace(config.ProfilesIngestionEndpoint_DatadogApiKey)
                ? null
                : config.ProfilesIngestionEndpoint_DatadogApiKey.Trim();

            _ddDataTags_Host = ConfigGuard.ApplyNotNullOrUnspecified(config.DDDataTags_Host);
            _ddDataTags_Service = ConfigGuard.ApplyNotNullOrUnspecified(config.DDDataTags_Service);
            _ddDataTags_Env = ConfigGuard.ApplyNotNullOrUnspecified(config.DDDataTags_Env);
            _ddDataTags_Version = ConfigGuard.ApplyNotNullOrUnspecified(config.DDDataTags_Version);
            _ddDataTags_CustomTags = ConstructSanitizedCustomDataTagsList(config);
            _localFilesProfilesExporter = new LocalFilesProfilesExporter(config);
            _exportStatistics = new ExportStatistics(DateTimeOffset.MinValue);
            _metricsSender = CreateMetricsSender(config);
            _frameKinds_Native_IsEnabled = config.FrameKinds_Native_IsEnabled;
            _tryResolveStackFrameSymbolsDelegate = this.TryResolveStackFrameSymbols;
        }

        protected override string GetLoopThreadName()
        {
            return "DD.Profiler." + nameof(ResolveAndExportStacksBackgroundLoop);
        }

        protected override void Dispose(bool disposing)
        {
            _httpClient.Dispose();
            base.Dispose(disposing);
        }

        protected override TimeSpan GetPeriod()
        {
            return _exportInterval;
        }

        protected override void OnShutdownCompleted()
        {
            // Export one last time (aka Flush).
            FetchAndExportCompletedStackSnapshots();
        }

        protected override void PerformIterationWork()
        {
            FetchAndExportCompletedStackSnapshots();
        }

        protected override bool IsReady()
        {
            var completedStackSnapshots = _profilerEngine.GetCompletedStackSnapshots();
            if (completedStackSnapshots == null)
            {
                return false;
            }

            if ((completedStackSnapshots.TotalByteCount <= _totalByteCountThreshold) &&
                (completedStackSnapshots.TotalSnapshotsCount <= _totalSnapshotsThreshold))
            {
                return false;
            }

            _exportStatistics.IncEarlyTriggers();
            return true;
        }

        private static string ConstructProfilesIngestionEndpointUrl(IProductConfiguration config)
        {
            if (!string.IsNullOrWhiteSpace(config.ProfilesIngestionEndpoint_Url))
            {
                return config.ProfilesIngestionEndpoint_Url;
            }

            Validate.NotNullOrWhitespace(
                        config.ProfilesIngestionEndpoint_Host,
                        "Specified " + nameof(IProductConfiguration) + "." + nameof(IProductConfiguration.ProfilesIngestionEndpoint_Host));

            Validate.NotNullOrWhitespace(
                        config.ProfilesIngestionEndpoint_ApiPath,
                        "Specified " + nameof(IProductConfiguration) + "." + nameof(IProductConfiguration.ProfilesIngestionEndpoint_ApiPath));

            // Remove '/' from the end of Host:
            string host = config.ProfilesIngestionEndpoint_Host;
            while (host.EndsWith("/"))
            {
                host = host.Substring(0, host.Length - 1);
            }

            // ProfilesIngestionEndpoint_Port <= 0 means do not specify Port explicitly and use the default for the protocol:
            string port = (config.ProfilesIngestionEndpoint_Port <= 0)
                                ? string.Empty
                                : $":{config.ProfilesIngestionEndpoint_Port}";

            // Remove '/' from the start of ApiPath:
            string apiPath = config.ProfilesIngestionEndpoint_ApiPath;
            while (apiPath.StartsWith("/"))
            {
                apiPath = apiPath.Substring(1, host.Length - 1);
            }

            // If Host is NOT prefixed with a protocol spec, we assume HTTP.
            // If Host IS prefixed with HTTP or HTTPS, we use whatever is specified.
            // We would prefer to have HTTPS as default, but typically, the agent installation is LOCAL and does not interact with certificates.
            // Remote endpoints should not accept unsecured HTTP connections at all.
            string protocolPrefix;
            if (host.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                protocolPrefix = string.Empty;
            }
            else
            {
                protocolPrefix = "http://";
            }

            string endpointUrl = protocolPrefix + host + port + "/" + apiPath;
            return endpointUrl;
        }

        private static IReadOnlyList<KeyValuePair<string, string>> ConstructSanitizedCustomDataTagsList(IProductConfiguration config)
        {
            var customTags = new List<KeyValuePair<string, string>>();

            if (config.DDDataTags_CustomTags != null)
            {
                foreach (KeyValuePair<string, string> tag in config.DDDataTags_CustomTags)
                {
                    string tagKey = tag.Key?.Trim();

                    if (string.IsNullOrWhiteSpace(tagKey))
                    {
                        Log.Info(LogComponentMoniker, $"Skipping a tag from config setting \"{nameof(config.DDDataTags_CustomTags)}\" because its key is null or white-space");
                    }
                    else if (tagKey.Equals("host", StringComparison.OrdinalIgnoreCase)
                            || tagKey.Equals("service", StringComparison.OrdinalIgnoreCase)
                            || tagKey.Equals("env", StringComparison.OrdinalIgnoreCase)
                            || tagKey.Equals("version", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Info(
                            LogComponentMoniker,
                            $"Skipping a tag with key \"{tagKey}\" from config setting \"{nameof(config.DDDataTags_CustomTags)}\" because it uses a reserved key moniker.");
                    }
                    else
                    {
                        customTags.Add(new KeyValuePair<string, string>(tagKey, ConfigGuard.ApplyNotNullOrUnspecified(tag.Value)));
                    }
                }
            }

            return customTags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static LocationDescriptor CreateNewLocationDescriptor(StackFrameCodeKind codeKind, ulong frameInfoCode)
        {
            switch (codeKind)
            {
                case StackFrameCodeKind.ClrManaged:
                case StackFrameCodeKind.UnknownNative:
                    return new LocationDescriptor(locationKind: (byte)codeKind, locationInfoCode: frameInfoCode);

                // For these we may add something smarter in the future:
                case StackFrameCodeKind.ClrNative:
                case StackFrameCodeKind.UserNative:
                case StackFrameCodeKind.Kernel:
                case StackFrameCodeKind.MultipleMixed:
                case StackFrameCodeKind.Dummy:
                    return new LocationDescriptor(locationKind: (byte)codeKind, locationInfoCode: 0);

                // These are legal values, but this should never occur at this point:
                // (we log this as Debug, not as Error, because they can be very voluminious and we do not want to blow up the log file)
                case StackFrameCodeKind.NotDetermined:
                    if (Log.IsDebugLoggingEnabled)
                    {
                        Log.Debug(
                            Log.WithCallInfo(LogComponentMoniker),
                            "This " + nameof(StackFrameCodeKind) + " should never occur here.",
                            nameof(StackFrameCodeKind),
                            codeKind);
                    }

                    return new LocationDescriptor(locationKind: (byte)codeKind, locationInfoCode: 0);

                // For these, there is nothing we can do whatsoever:
                case StackFrameCodeKind.Unknown:
                default:
                    return new LocationDescriptor(locationKind: (byte)codeKind, locationInfoCode: 0);
            }
        }

        private static string ObfuscateDdApiKey(string apiKey)
        {
            // A real API ley is longer than 9 chars. A short key is probably some kind of placeholder, so we do not need to obfuscate it.
            if (apiKey == null)
            {
                return null;
            }

            if (apiKey == null || apiKey.Length <= 9)
            {
                return $"{apiKey} (unchanged)";
            }

            return $"{apiKey.Substring(0, 3)}...{apiKey.Substring(apiKey.Length - 3)} (shortened; original length={apiKey.Length})";
        }

        private static void ResolveStackFrameSymbolsForClrManaged(
                                ulong clrFunctionId,
                                out string functionName,
                                out string typeName,
                                out string assemblyName,
                                out string assemblyVersion)
        {
            IntPtr pFunctionName = IntPtr.Zero;
            IntPtr pTypeName = IntPtr.Zero;
            IntPtr pAssemblyName = IntPtr.Zero;

            NativeInterop.TryResolveStackFrameSymbols(
                            StackFrameCodeKind.ClrManaged,
                            clrFunctionId,
                            ref pFunctionName,
                            ref pTypeName,
                            ref pAssemblyName);

            functionName = Marshal.PtrToStringUni(pFunctionName);
            typeName = Marshal.PtrToStringUni(pTypeName);
            assemblyName = Marshal.PtrToStringUni(pAssemblyName);
            assemblyVersion = null;
        }

        private static bool ResolveStackFrameSymbolsForUnknownNative(
                                ulong moduleHandle,
                                out string functionName,
                                out string classTypeName,
                                out string binaryContainerName,
                                out string binaryContainerVersion)
        {
            if (!NativeInterop.TryGetModuleBaseName(moduleHandle, out string moduleBaseName))
            {
                functionName = null;
                classTypeName = null;
                binaryContainerName = null;
                binaryContainerVersion = null;

                return false;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(moduleBaseName))
                {
                    moduleBaseName = "Unknown-Native-Module";
                }

                functionName = "Function";
                classTypeName = $"|ns:NativeCode |ct:{moduleBaseName}";
                binaryContainerName = moduleBaseName;
                binaryContainerVersion = null;

                return true;
            }
        }

        private MetricsSender CreateMetricsSender(IProductConfiguration config)
        {
            if (!config.Metrics_Operational_IsEnabled)
            {
                return null;
            }

            try
            {
                return new MetricsSender(config);
            }
            catch (Exception ex)
            {
                Log.Error(LogComponentMoniker, ex, "Failed to create a metric sender instance.");
                return null;
            }
        }

        /// <summary>
        /// The main routine. Fetch snapshots and export them.
        /// It is invoked once per _exportInterval or when IsReady() returns true.
        /// </summary>
        private void FetchAndExportCompletedStackSnapshots()
        {
            // NOTE: It might happen that the CLR notify the native profiler that it shuts down BEFORE managed threads actually ends.
            //       It means that it can occur while a profile is generated (because the current thread is a background thread)
            //       To avoid managed code calling native code through P/Invoke in such a state (where it is not allowed to call
            //       ICorProfilerInfo method to get a frame managed method name for example), all P/Invoke calls are "protected"
            //       and will throw a ClrShutdownException if it happens; stopping the profile generation.
            try
            {
                // Flush the current segment used to store the stack snapshots
                NativeInterop.TryCompleteCurrentWriteSegment();

                // Fetch the snapshots as a collection of complete segments committed by the native engine since the last invocation.
                StackSnapshotsBufferSegmentCollection completedStackSnapshots = _profilerEngine.GetResetCompletedStackSnapshots();
                if (completedStackSnapshots == null)
                {
                    // No snapshots should give us an empty collection. Null means we are currently shutting down.
                    return;
                }

                // TotalSnapshotsCount refers to the number of shapshots in the segments that are completed/comitted by the native engine.
                if (completedStackSnapshots.TotalSnapshotsCount == 0)
                {
                    // completedStackSnapshots collection is empty. Dispose it:
                    completedStackSnapshots.Dispose();
                    completedStackSnapshots = null;

                    _exportStatistics.IncExportsSkippedDueToNoData();

                    Log.Debug(LogComponentMoniker, "No snapshot to export...");

                    return;
                }

                try
                {
                    _exportStatistics.IncTotalExports();

                    // if ExportCompletedStackSnapshots(..) throws, this will remain False
                    // otherwise, it will be True or False depending on results

                    bool exportSuccess = false;
                    try
                    {
                        exportSuccess = ExportCompletedStackSnapshots(completedStackSnapshots);
                    }
                    finally
                    {
                        SendExportsMetric(exportSuccess);
                        if (exportSuccess)
                        {
                            _exportStatistics.IncSuccessExports();
                            _totalPProfSuccessExportsCount++;
                        }
                        else
                        {
                            _exportStatistics.IncFailureExports();
                        }
                    }
                }
                finally
                {
                    completedStackSnapshots.Dispose();
                    completedStackSnapshots = null;

                    DateTimeOffset now = DateTimeOffset.Now;
                    TimeSpan thisAggregationPeriodLength = now - _exportStatistics.AggregationPeriodStartTimestamp;
                    if (thisAggregationPeriodLength > ExportStatistics.AggregationPeriodLength)
                    {
                        var newExportStats = new ExportStatistics(now);
                        ExportStatistics completedExportStats = Interlocked.Exchange(ref _exportStatistics, newExportStats);

#pragma warning disable SA1117 // easier to have the mapping key/value on the same line
                        Log.Info(
                            LogComponentMoniker,
                            "Profile data export statistics",
                            "Lifetime total SuccessExports", _totalPProfSuccessExportsCount,
                            "Target" + nameof(ExportStatistics) + "." + nameof(ExportStatistics.AggregationPeriodLength), ExportStatistics.AggregationPeriodLength,
                            nameof(ExportStatistics.AggregationPeriodStartTimestamp), completedExportStats.AggregationPeriodStartTimestamp,
                            "This AggregationPeriodLength", thisAggregationPeriodLength,
                            nameof(ExportStatistics.EarlyTriggers), completedExportStats.EarlyTriggers,
                            nameof(ExportStatistics.ExportsSkippedDueToNoData), completedExportStats.ExportsSkippedDueToNoData,
                            nameof(ExportStatistics.TotalExports), completedExportStats.TotalExports,
                            nameof(ExportStatistics.SuccessExports), completedExportStats.SuccessExports,
                            nameof(ExportStatistics.FailureExports), completedExportStats.FailureExports,
                            nameof(ExportStatistics.TotalSamples), completedExportStats.TotalSamples,
                            nameof(ExportStatistics.TotalCompressedDataBytes), completedExportStats.TotalCompressedDataBytes,
                            nameof(ExportStatistics.TotalUncompressedDataBytes), completedExportStats.TotalUncompressedDataBytes);
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
                    }
                }
            }
            catch (ClrShutdownException x)
            {
                Log.Info(LogComponentMoniker, $"Impossible to finish profile generation: {x.Message}");
            }
        }

        private void SendMetric(string metricName, long value, string[] tags = null)
        {
            if (_metricsSender == null)
            {
                return;
            }

            try
            {
                _metricsSender.SendMetric(metricName, value, tags);
            }
            catch (Exception ex)
            {
#pragma warning disable SA1117 // easier to have the mapping key/value on the same line
                Log.Error(LogComponentMoniker, ex, "An exception occured when sending metric.",
                          nameof(metricName), metricName,
                          nameof(value), value,
                          nameof(tags), tags);
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
            }
        }

        private void SendExportsMetric(bool exportSuccess)
        {
            // check here to prevent from creating un-necessary array (ex: tags)
            if (_metricsSender == null)
            {
                return;
            }

            string metricName = "datadog.profiling.dotnet.operational.exports";
            long value = 1;
            string successTag = "success:" + exportSuccess;
            var tags = new string[] { successTag };

            SendMetric(metricName, value, tags);
        }

        private bool ExportCompletedStackSnapshots(StackSnapshotsBufferSegmentCollection stackSnapshotsBuffer)
        {
            // If required, dump the snapshot data to the screen:
            if (DebugOptions.PrintRawStackSnapshotsBuffer)
            {
                PrintSegmentCollectionForDebug(stackSnapshotsBuffer);
            }

            // start to measure pprof generation duration
            DateTimeOffset startTime = DateTimeOffset.Now;

            ProfiledThreadInfoProvider profiledThreadInfoProvider = _profilerEngine.GetProfiledThreadInfoProvider();
            profiledThreadInfoProvider.StartNextSession();

            ProfiledAppDomainProvider profiledAppDomainProvider = _profilerEngine.GetProfiledAppDomainProvider();
            profiledAppDomainProvider.StartNextSession();

            PProfBuilder pprofBuilder = _profilerEngine.GetPProfBuilder();

            pprofBuilder.TrySetMainEntrypointMappingInfo(
                                entrypointBinaryContainerName: Assembly.GetEntryAssembly()?.GetName()?.Name,
                                entrypointBinaryContainerVersion: null);

            pprofBuilder.SetSampleValueTypes(
                            new PProfSampleValueType("wall", "nanoseconds"),
                            new PProfSampleValueType("metric", "unit"));   // DEBUG, remove later

            pprofBuilder.DropFramesRegExp = "Drop-These-Frames";    // DEBUG, remove later
            pprofBuilder.KeepFramesRegExp = "Keep-These-Frames";    // DEBUG, remove later
            pprofBuilder.PeriodType = new PProfSampleValueType("RealTime", "Nanoseconds");

            using (PProfBuildSession pprofBuildSession = pprofBuilder.StartNewPProfBuildSession())
            {
                pprofBuildSession.Timestamp = stackSnapshotsBuffer.TotalTimeRangeStart;
                pprofBuildSession.Duration = stackSnapshotsBuffer.TotalTimeRangeEnd - stackSnapshotsBuffer.TotalTimeRangeStart;
                pprofBuildSession.Period = 1;
                pprofBuildSession.SetComment($"This is PProf Session Number {_totalPProfSuccessExportsCount + 1}."); // DEBUG, remove later

                foreach (StackSnapshotsBufferSegment snapshotsBufferSegment in stackSnapshotsBuffer.Segments)
                {
                    if (snapshotsBufferSegment == null && snapshotsBufferSegment.SnapshotsCount == 0)
                    {
                        continue;
                    }

                    StackSnapshotsBufferSegment.SnapshotEnumerator snapshots = snapshotsBufferSegment.EnumerateSnapshots();
                    do
                    {
                        StackSnapshotResult snapshot = snapshots.GetCurrent();
                        pprofBuildSession.AddNextSample();

                        ulong representedNanosecs = snapshot.GetRepresentedDurationNanosecondsUnsafe();
                        uint profilerThreadInfoId = snapshot.GetProfilerThreadInfoIdUnsafe();
                        ulong profilerAppDomainId = snapshot.GetProfilerAppDomainIdUnsafe();
                        ushort framesCount = snapshot.GetFramesCountUnsafe();

                        for (ushort f = 0; f < framesCount; f++)
                        {
                            snapshot.GetFrameAtIndexUnsafe(f, out StackFrameCodeKind codeKind, out ulong frameInfoCode);

                            pprofBuildSession.TryAddLocationToLastSample(
                                                CreateNewLocationDescriptor(codeKind, frameInfoCode),
                                                _tryResolveStackFrameSymbolsDelegate);
                        }

                        var labels = new List<PProfSampleLabel>(capacity: 4);

                        // Adding more labels?
                        // Add the labels below and adjust CAPACITY above!
                        // Note that the backend requires that frame labels describing numeric IDs are serialized as string-typed (rather than number-typed) labels.
                        bool hasThreadInfo = profiledThreadInfoProvider.TryGetProfiledEntityInfo(profilerThreadInfoId, out ProfiledThreadInfo threadInfo);
                        string threadIdStr = hasThreadInfo ? threadInfo.ThreadIdString : ProfiledThreadInfo.FormatIdForUnknownThread(profilerThreadInfoId);
                        string threadDescription = hasThreadInfo ? threadInfo.ThreadDescription : ProfiledThreadInfo.FormatDescriptionForUnknownThread(profilerThreadInfoId);

                        labels.Add(new PProfSampleLabel(key: "thread id", str: threadIdStr));
                        labels.Add(new PProfSampleLabel(key: "thread name", str: threadDescription));

                        bool hasAppDomainInfo = profiledAppDomainProvider.TryGetProfiledEntityInfo(profilerAppDomainId, out ProfiledAppDomainInfo appDomainInfo);
                        string appDomainName = hasAppDomainInfo ? appDomainInfo.AppDomainName : ProfiledAppDomainInfo.FormatNameForUnknownAppDomain(profilerAppDomainId);
                        string appDomainProcessIdStr = hasAppDomainInfo ? appDomainInfo.AppDomainProcessIdString : ProfiledAppDomainInfo.FormatProcessIdForUnknownAppDomain(profilerAppDomainId);

                        labels.Add(new PProfSampleLabel(key: "appdomain name", str: appDomainName));
                        labels.Add(new PProfSampleLabel(key: "appdomain process id", str: appDomainProcessIdStr));

                        pprofBuildSession.SetSampleLabels(labels);
                        pprofBuildSession.SetSampleValues((long)representedNanosecs, 42);  // DEBUG, remove later
                    }
                    while (snapshots.MoveNext());
                }

                var duration = DateTimeOffset.Now - startTime;
                return ExportProfiles(pprofBuildSession, stackSnapshotsBuffer.Segments.Count, duration);
            }
        }

        private bool TryResolveStackFrameSymbols(
                        PProfBuildSession pprofBuildSession,
                        LocationDescriptor locationDescriptor,
                        out string functionName,
                        out string classTypeName,
                        out string binaryContainerName,
                        out string binaryContainerVersion)
        {
            // This is called back from the PProf Build Session when the stack frame symbols are not in its cache and need to be resolved.

            StackFrameCodeKind codeKind = (StackFrameCodeKind)locationDescriptor.LocationKind;
            ulong frameInfoCode = locationDescriptor.LocationInfoCode;

            bool isNativeFrame = (codeKind == StackFrameCodeKind.ClrNative)
                                    || (codeKind == StackFrameCodeKind.UserNative)
                                    || (codeKind == StackFrameCodeKind.UnknownNative)
                                    || (codeKind == StackFrameCodeKind.Kernel);

            if (isNativeFrame && !_frameKinds_Native_IsEnabled)
            {
                functionName = classTypeName = binaryContainerName = binaryContainerVersion = null;
                return false;
            }

            switch (codeKind)
            {
                case StackFrameCodeKind.ClrManaged:
                    // TODO: how to handle error?
                    // functionName should be null...
                    ResolveStackFrameSymbolsForClrManaged(frameInfoCode, out functionName, out classTypeName, out binaryContainerName, out binaryContainerVersion);
                    return true;

                // For these we may add something smarter in the future:
                case StackFrameCodeKind.ClrNative:
                    functionName = "InternalFunction";
                    classTypeName = "|ns:CommonLanguageRuntime |ct:Clr.NativeCode";
                    binaryContainerName = pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownBinaryContainerName;
                    binaryContainerVersion = null;
                    return true;

                case StackFrameCodeKind.UserNative:
                    functionName = "Function";
                    classTypeName = "|ns:User.Application |ct:NativeCode";
                    binaryContainerName = pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownBinaryContainerName;
                    binaryContainerVersion = null;
                    return true;

                case StackFrameCodeKind.UnknownNative:
                    if (!ResolveStackFrameSymbolsForUnknownNative(frameInfoCode, out functionName, out classTypeName, out binaryContainerName, out binaryContainerVersion))
                    {
                        functionName = "Function";
                        classTypeName = "|ns:Unknown |ct:NativeCode";
                        binaryContainerName = pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownBinaryContainerName;
                        binaryContainerVersion = null;
                    }

                    return true;

                case StackFrameCodeKind.Kernel:
                    functionName = "Function";
                    classTypeName = "|ns:KernelMode |ct:Kernel";
                    binaryContainerName = pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownBinaryContainerName;
                    binaryContainerVersion = null;
                    return true;

                case StackFrameCodeKind.MultipleMixed:
                    functionName = "Multiple-Stack-Frames";
                    classTypeName = "|ns: |ct:PLACEHOLDER";
                    binaryContainerName = "Multiple-Stack-Frames";
                    binaryContainerVersion = null;
                    return true;

                case StackFrameCodeKind.Dummy:
                    string osPlatform = RuntimeEnvironmentInfo.SingeltonInstance.OsPlatform;
                    if ("Windows".Equals(osPlatform, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Error(LogComponentMoniker, $"Frame of kind {codeKind} encountered on OS \"{osPlatform}\".");
                        functionName = "Dummy-Function";
                        classTypeName = "|ns:Dummy-Namespace |ct:Dummy-Type";
                        binaryContainerName = "Dummy-Assembly";
                        binaryContainerVersion = null;
                        return true;
                    }
                    else
                    {
                        osPlatform = osPlatform ?? "Unknown";
                        osPlatform = osPlatform.Replace('|', '_').Replace(':', '_').Replace(' ', '_');
                        functionName = "Not.Supported";
                        classTypeName = $"|ns:Unsupported.OperatingSystem |ct:{osPlatform}-OS";
                        binaryContainerName = "Dummy-Assembly";
                        binaryContainerVersion = null;
                        return true;
                    }

                // For these, there is not much more we can do:
                case StackFrameCodeKind.Unknown:
                case StackFrameCodeKind.NotDetermined:
                default:
                    functionName = pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownFunctionName;
                    classTypeName = pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownClassTypeName;
                    binaryContainerName = $"[StackFrameCodeKind:{codeKind.ToString()}] {pprofBuildSession.OwnerBuilder.SymbolMonikersConfig.UnknownBinaryContainerName}";
                    binaryContainerVersion = null;
                    return true;
            }
        }

        private bool ExportProfiles(PProfBuildSession pprofBuildSession, int segmentsCount, TimeSpan generationDuration)
        {
            bool exportSuccess = true;
            var errors = default(ExceptionAggregator);

            try
            {
                if (_localFilesProfilesExporter.IsEnabled)
                {
                    exportSuccess &= _localFilesProfilesExporter.ExportProfiles(pprofBuildSession);
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            try
            {
                exportSuccess &= ExportProfilesToIngestionEndpointViaHttp(pprofBuildSession, segmentsCount, generationDuration);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            errors.ThrowIfNotEmpty("Multiple errors ocurred during profiles export");
            return exportSuccess;
        }

        private bool ExportProfilesToIngestionEndpointViaHttp(PProfBuildSession pprofBuildSession, int segmentsCount, TimeSpan generationDuration)
        {
            EfficientHttpClient.MultipartFormPostRequest request = _httpClient.CreateNewMultipartFormPostRequest(_profilesIngestionEndpoint_url);

            if (_profilesIngestionEndpoint_apiKey != null)
            {
                request.AddHeader("DD-API-KEY", _profilesIngestionEndpoint_apiKey);
            }

            const string TimestampFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'";
            DateTimeOffset startTimestamp = pprofBuildSession.Timestamp.ToUniversalTime();
            DateTimeOffset endTimestamp = (pprofBuildSession.Timestamp + pprofBuildSession.Duration).ToUniversalTime();

            request.AddPlainTextFormPart("start", startTimestamp.ToString(TimestampFormat));
            request.AddPlainTextFormPart("end", endTimestamp.ToString(TimestampFormat));

            request.AddPlainTextFormPart("version", "3");
            request.AddPlainTextFormPart("family", HttpProfilesExportMetadata.Family);
            request.AddPlainTextFormPart("tags[]", $"language:{HttpProfilesExportMetadata.LanguageTag}");
            request.AddPlainTextFormPart("tags[]", $"profiler_version:{_profilerEngine.VersionInfo.InformationalVersion}");
            request.AddPlainTextFormPart("tags[]", $"pid:{CurrentProcess.GetId()}");

            request.AddPlainTextFormPart("tags[]", $"host:{_ddDataTags_Host}");
            request.AddPlainTextFormPart("tags[]", $"service:{_ddDataTags_Service}");
            request.AddPlainTextFormPart("tags[]", $"env:{_ddDataTags_Env}");
            request.AddPlainTextFormPart("tags[]", $"version:{_ddDataTags_Version}");

            for (int i = 0; i < _ddDataTags_CustomTags.Count; i++)
            {
                KeyValuePair<string, string> customTag = _ddDataTags_CustomTags[i];
                request.AddPlainTextFormPart("tags[]", $"{customTag.Key}:{customTag.Value}");
            }

            RuntimeEnvironmentInfo runtimeInfo = RuntimeEnvironmentInfo.SingeltonInstance;

            request.AddPlainTextFormPart("tags[]", $"runtime_version:{DespacifyTag(runtimeInfo.RuntimeName)}_{DespacifyTag(runtimeInfo.RuntimeVersion)}");
            request.AddPlainTextFormPart("tags[]", $"runtime_platform:{runtimeInfo.GetOsPlatformMoniker()}_{Environment.OSVersion.Version.ToString()}_{runtimeInfo.OsArchitecture}");
            request.AddPlainTextFormPart("tags[]", $"process_architecture:{DespacifyTag(runtimeInfo.ProcessArchitecture)}");

            ulong compressedDataBytes = 0;
            ulong uncompressedDataBytes = 0;

            const string PprofContentName = "auto.pprof";
            using (Stream datas = request.AddOctetStreamFormPart(name: $"data[{PprofContentName}]", filename: PprofContentName))
            {
                using (var outs = new WriteOnlyStream(
                                        new GZipStream(datas, PProfHttpExportGZipCompressionLevel),
                                        leaveUnderlyingStreamOpenWhenDisposed: false))
                {
                    pprofBuildSession.WriteProfileToStream(outs);

                    uncompressedDataBytes = (ulong)Math.Max(0, outs.WrittenBytes);
                }

                if (datas is WriteOnlyStream dataStream)
                {
                    compressedDataBytes = (ulong)Math.Max(0, dataStream.WrittenBytes);
                }
            }

            _exportStatistics.IncTotalSamples(pprofBuildSession.SamplesCount);
            _exportStatistics.IncTotalCompressedDataBytes(compressedDataBytes);
            _exportStatistics.IncTotalUncompressedDataBytes(uncompressedDataBytes);

            DateTimeOffset startTime = DateTimeOffset.Now;
            EfficientHttpClient.Response response = request.Send();
            var duration = DateTimeOffset.Now - startTime;

#pragma warning disable SA1117 // easier to have the mapping key/value on the same line
            bool exportSuccess = (response.Error == null) && (response.StatusCode / 100 == 2);
            if (!exportSuccess)
            {
                Log.Error(
                        LogComponentMoniker,
                        "Profile data was NOT successfully exported via HTTP POST",
                        response.Error,
                        $"{nameof(response)}.{nameof(response.StatusCode)}", response.StatusCode,
                        $"{nameof(response)}.{nameof(response.StatusCodeString)}", response.StatusCodeString,
                        $"{nameof(response)}.{nameof(response.Payload)}", response.Payload,
                        nameof(_profilesIngestionEndpoint_url), _profilesIngestionEndpoint_url,
                        "DD-API-KEY", ObfuscateDdApiKey(_profilesIngestionEndpoint_apiKey),
                        "SegmentsCount", segmentsCount,
                        $"{nameof(pprofBuildSession)}.{nameof(pprofBuildSession.Timestamp)}", pprofBuildSession.Timestamp,
                        $"{nameof(pprofBuildSession)}.{nameof(pprofBuildSession.Duration)}", pprofBuildSession.Duration,
                        $"{nameof(pprofBuildSession)}.{nameof(pprofBuildSession.SamplesCount)}", pprofBuildSession.SamplesCount,
                        "Generation.Duration", generationDuration,
                        "Send.Duration", duration,
                        nameof(compressedDataBytes), compressedDataBytes,
                        nameof(uncompressedDataBytes), uncompressedDataBytes);
            }
            else
            {
                Log.Debug(
                        LogComponentMoniker,
                        "Profile data was exported via HTTP POST",
                        "Lifetime total SuccessExports", _totalPProfSuccessExportsCount,
                        $"{nameof(response)}.{nameof(response.StatusCode)}", response.StatusCode,
                        $"{nameof(response)}.{nameof(response.StatusCodeString)}", response.StatusCodeString,
                        $"{nameof(response)}.{nameof(response.Payload)}", response.Payload,
                        $"{nameof(response)}.{nameof(response.Error)}", response.Error?.ToString() ?? "None",
                        nameof(_profilesIngestionEndpoint_url), _profilesIngestionEndpoint_url,
                        "DD-API-KEY", ObfuscateDdApiKey(_profilesIngestionEndpoint_apiKey),
                        "Segments.Count", segmentsCount,
                        $"{nameof(pprofBuildSession)}.{nameof(pprofBuildSession.Timestamp)}", pprofBuildSession.Timestamp,
                        $"{nameof(pprofBuildSession)}.{nameof(pprofBuildSession.Duration)}", pprofBuildSession.Duration,
                        $"{nameof(pprofBuildSession)}.{nameof(pprofBuildSession.SamplesCount)}", pprofBuildSession.SamplesCount,
                        "Generation.Duration", generationDuration,
                        "Send.Duration", duration,
                        nameof(compressedDataBytes), compressedDataBytes,
                        nameof(uncompressedDataBytes), uncompressedDataBytes);
            }
#pragma warning restore SA1117 // Parameters should be on same line or separate lines

            return exportSuccess;
        }

        private string DespacifyTag(string tagValue)
        {
            return tagValue?.Trim()?.Replace(' ', '-');
        }

        private void PrintSegmentCollectionForDebug(StackSnapshotsBufferSegmentCollection stackSnapshotsBuffer)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"*********** Exporting Stack Snapshots: ***********");
                Console.WriteLine($"*********** Number of segments:  {stackSnapshotsBuffer.Segments.Count}.");
                Console.WriteLine($"*********** Number of snapshots: {stackSnapshotsBuffer.TotalSnapshotsCount}.");
                Console.WriteLine($"*********** Total bytes used:    {stackSnapshotsBuffer.TotalByteCount}"
                                + $" ({Math.Round(stackSnapshotsBuffer.TotalByteCount / (1024.0 * 1024.0), 3)} MB).");
                Console.WriteLine($"*********** Total duration:      {stackSnapshotsBuffer.TotalTimeRangeEnd - stackSnapshotsBuffer.TotalTimeRangeStart}"
                                + $" ({Format.AsReadablePreciseLocal(stackSnapshotsBuffer.TotalTimeRangeStart)}"
                                + $" ... {Format.AsReadablePreciseLocal(stackSnapshotsBuffer.TotalTimeRangeEnd)}).");

                foreach (StackSnapshotsBufferSegment stackSnapshots in stackSnapshotsBuffer.Segments)
                {
                    PrintSegmentForDebug(stackSnapshots);
                }

                Console.WriteLine($"*********** Total duration:      {stackSnapshotsBuffer.TotalTimeRangeEnd - stackSnapshotsBuffer.TotalTimeRangeStart}"
                                + $" ({Format.AsReadablePreciseLocal(stackSnapshotsBuffer.TotalTimeRangeStart)}"
                                + $" ... {Format.AsReadablePreciseLocal(stackSnapshotsBuffer.TotalTimeRangeEnd)}).");
                Console.WriteLine($"*********** Total bytes used:    {stackSnapshotsBuffer.TotalByteCount}"
                                + $" ({Math.Round(stackSnapshotsBuffer.TotalByteCount / (1024.0 * 1024.0), 3)} MB).");
                Console.WriteLine($"*********** Number of snapshots: {stackSnapshotsBuffer.TotalSnapshotsCount}.");
                Console.WriteLine($"*********** Number of segments:  {stackSnapshotsBuffer.Segments.Count}.");
                Console.WriteLine($"*********** Exporting Stack Snapshots done. ***********");
                Console.WriteLine();
            }
            catch (ClrShutdownException x)
            {
                Console.WriteLine(x.Message);
            }
        }

        private void PrintSegmentForDebug(StackSnapshotsBufferSegment stackSnapshots)
        {
            Console.WriteLine($"***** Segment covering time range {Format.AsReadablePreciseLocal(stackSnapshots.TimeRangeStart)}"
                            + $" ... {Format.AsReadablePreciseLocal(stackSnapshots.TimeRangeStart)} has {stackSnapshots.SnapshotsCount} snapshot(s). *****");

            if (stackSnapshots.SnapshotsCount == 0)
            {
                return;
            }

            if (!DebugOptions.PrintRawStackSnapshotsBufferIncludeDetails)
            {
                return;
            }

            StackSnapshotsBufferSegment.SnapshotEnumerator snapshots = stackSnapshots.EnumerateSnapshots();
            do
            {
                StackSnapshotResult snapshot = snapshots.GetCurrent();

                Console.WriteLine();
                Console.WriteLine($"***** Snapshot {snapshots.CurrentSnapshotIndex + 1} of {stackSnapshots.SnapshotsCount}"
                                + $" in segment covering time range {Format.AsReadablePreciseLocal(stackSnapshots.TimeRangeStart)}"
                                + $" ... {Format.AsReadablePreciseLocal(stackSnapshots.TimeRangeStart)}. *****");

                ulong representedNanosecs = snapshot.GetRepresentedDurationNanosecondsUnsafe();
                uint profilerThreadInfoId = snapshot.GetProfilerThreadInfoIdUnsafe();
                ushort framesCount = snapshot.GetFramesCountUnsafe();

                Console.WriteLine($"***** Snapshot {snapshots.CurrentSnapshotIndex + 1} of {stackSnapshots.SnapshotsCount}"
                                + $" on thread-info-id {profilerThreadInfoId}"
                                + $" covering {representedNanosecs} nanosec and having {framesCount} frames: *****");

                for (ushort f = 0; f < framesCount; f++)
                {
                    snapshot.GetFrameAtIndexUnsafe(f, out StackFrameCodeKind codeKind, out ulong frameInfoCode);

                    IntPtr pFunctionName = IntPtr.Zero;
                    IntPtr pContainingTypeName = IntPtr.Zero;
                    IntPtr pContainingAssemblyName = IntPtr.Zero;

                    NativeInterop.TryResolveStackFrameSymbols(
                                    codeKind,
                                    frameInfoCode,
                                    ref pFunctionName,
                                    ref pContainingTypeName,
                                    ref pContainingAssemblyName);

                    string functionName = Marshal.PtrToStringUni(pFunctionName);
                    string containingTypeName = Marshal.PtrToStringUni(pContainingTypeName);
                    string containingAssemblyName = Marshal.PtrToStringUni(pContainingAssemblyName);

                    Console.WriteLine($"    [{f.ToString("D3")}] "
                                    + Format.EnsureMinLength($"{codeKind}: ", 15)
                                    + $" {containingAssemblyName}::{containingTypeName}::{functionName}");
                }
            }
            while (snapshots.MoveNext());
        }

        private static class HttpProfilesExportMetadata
        {
            public const string MetadataProtocolVersion = "3";
            public const string Family = "dotnet";
            public const string LanguageTag = "dotnet";
        }

        private static class ConfigGuard
        {
            public const ulong BytesMin = 1024;              // 1 KByte
            public const ulong BytesMax = 100 * 1024 * 1024; // 100 MByte
            public const ulong CountMin = 10;
            public const ulong CountMax = 500000;
            public const string UnspecifiedMoniker = "Unspecified";
            public static readonly TimeSpan ExportIntervalMin = TimeSpan.FromMilliseconds(100);
            public static readonly TimeSpan ExportIntervalMax = TimeSpan.FromMinutes(5);

            public static T ApplyInRange<T>(T value, T min, T max)
                where T : IComparable
            {
                if (value.CompareTo(min) < 0)
                {
                    return min;
                }
                else if (value.CompareTo(max) > 0)
                {
                    return max;
                }
                else
                {
                    return value;
                }
            }

            public static string ApplyNotNullOrUnspecified(string value)
            {
                return value ?? UnspecifiedMoniker;
            }
        }

        private class DebugOptions
        {
            // Use "static readonly" instead of "const" to avoid Unreachable Code warnings.
            public static readonly bool PrintRawStackSnapshotsBuffer = false;
            public static readonly bool PrintRawStackSnapshotsBufferIncludeDetails = true;
        }

        private class ExportStatistics
        {
#if DEBUG
            public static readonly TimeSpan AggregationPeriodLength = TimeSpan.FromSeconds(180);
#else
            public static readonly TimeSpan AggregationPeriodLength = TimeSpan.FromMinutes(30);
#endif
            private DateTimeOffset _aggregationPeriodStartTimestamp = DateTimeOffset.MinValue;
            private int _earlyTriggers = 0;
            private int _exportsSkippedDueToNoData = 0;
            private int _totalExports = 0;
            private int _successExports = 0;
            private int _failureExports = 0;
            private ulong _totalSamples = 0;
            private ulong _totalCompressedDataBytes = 0;
            private ulong _totalUncompressedDataBytes = 0;

            public ExportStatistics(DateTimeOffset aggregationPeriodStartTimestamp)
            {
                _aggregationPeriodStartTimestamp = aggregationPeriodStartTimestamp;
            }

            public DateTimeOffset AggregationPeriodStartTimestamp
            {
                get { return _aggregationPeriodStartTimestamp; }
            }

            public int EarlyTriggers
            {
                get { return _earlyTriggers; }
            }

            public int ExportsSkippedDueToNoData
            {
                get { return _exportsSkippedDueToNoData; }
            }

            public int TotalExports
            {
                get { return _totalExports; }
            }

            public int SuccessExports
            {
                get { return _successExports; }
            }

            public int FailureExports
            {
                get { return _failureExports; }
            }

            public ulong TotalSamples
            {
                get { return _totalSamples; }
            }

            public ulong TotalCompressedDataBytes
            {
                get { return _totalCompressedDataBytes; }
            }

            public ulong TotalUncompressedDataBytes
            {
                get { return _totalUncompressedDataBytes; }
            }

            public void IncEarlyTriggers()
            {
                _earlyTriggers++;
            }

            public void IncExportsSkippedDueToNoData()
            {
                _exportsSkippedDueToNoData++;
            }

            public void IncTotalExports()
            {
                _totalExports++;
            }

            public void IncSuccessExports()
            {
                _successExports++;
            }

            public void IncFailureExports()
            {
                _failureExports++;
            }

            public void IncTotalSamples(ulong count)
            {
                _totalSamples += count;
            }

            public void IncTotalCompressedDataBytes(ulong count)
            {
                _totalCompressedDataBytes += count;
            }

            public void IncTotalUncompressedDataBytes(ulong count)
            {
                _totalUncompressedDataBytes += count;
            }
        }
    }
}