// <copyright file="ExceptionDebugging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionDebugging
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionDebugging));

        private static ExceptionReplaySettings? _settings;
        private static int _firstInitialization = 1;
        private static bool _isDisabled;

        private static SnapshotUploader? _uploader;
        private static SnapshotSink? _snapshotSink;

        public static ExceptionReplaySettings Settings
        {
            get => LazyInitializer.EnsureInitialized(ref _settings, ExceptionReplaySettings.FromDefaultSource)!;
            private set => _settings = value;
        }

        public static bool Enabled => Settings.Enabled && !_isDisabled;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                return;
            }

            Log.Information("Initializing Exception Debugging");

            if (!ThirdPartyModules.IsValid)
            {
                Log.Warning("Third party modules load has failed. Disabling Exception Debugging.");
                _isDisabled = true;
            }
            else
            {
                InitSnapshotsSink();
                ExceptionTrackManager.Initialize();
                LifetimeManager.Instance.AddShutdownTask(Dispose);
            }
        }

        private static void InitSnapshotsSink()
        {
            var tracer = Tracer.Instance;
            var debuggerSettings = DebuggerSettings.FromDefaultSource();

            // Set configs relevant for DI and Exception Debugging, using DI's environment keys.
            DebuggerSnapshotSerializer.SetConfig(debuggerSettings);
            Redaction.SetConfig(debuggerSettings);

            // Set up the snapshots sink.
            var snapshotSlicer = SnapshotSlicer.Create(debuggerSettings);
            _snapshotSink = SnapshotSink.Create(debuggerSettings, snapshotSlicer);
            var apiFactory = AgentTransportStrategy.Get(
                tracer.Settings.ExporterInternal,
                productName: "debugger",
                tcpTimeout: TimeSpan.FromSeconds(15),
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);
            var discoveryService = tracer.TracerManager.DiscoveryService;
            var gitMetadataTagsProvider = tracer.TracerManager.GitMetadataTagsProvider;

            var snapshotUploadApi = DebuggerUploadApiFactory.CreateSnapshotUploadApi(apiFactory, discoveryService, gitMetadataTagsProvider);
            var snapshotBatchUploader = BatchUploader.Create(snapshotUploadApi);

            _uploader = SnapshotUploader.Create(
                snapshotSink: _snapshotSink,
                snapshotBatchUploader: snapshotBatchUploader,
                debuggerSettings);

            Task.Run(() => _uploader.StartFlushingAsync())
                .ContinueWith(t => Log.Error(t.Exception, "Error in flushing task"), TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void Report(Span span, Exception exception)
        {
            if (!Enabled)
            {
                return;
            }

            ExceptionTrackManager.Report(span, exception);
        }

        public static void BeginRequest()
        {
            if (!Enabled)
            {
                return;
            }

            var tree = ShadowStackHolder.EnsureShadowStackEnabled();
            tree.Clear();
            tree.Init();
            tree.IsInRequestContext = true;
        }

        public static void EndRequest()
        {
            if (!Enabled)
            {
                return;
            }

            ShadowStackHolder.ShadowStack?.Clear();
        }

        public static void AddSnapshot(string probeId, string snapshot)
        {
            if (_snapshotSink == null)
            {
                Log.Debug("The sink of the Exception Debugging is null. Skipping the reporting of the snapshot: {Snapshot}", snapshot);
                return;
            }

            _snapshotSink.Add(probeId, snapshot);
        }

        public static void Dispose(Exception? ex)
        {
            ExceptionTrackManager.Dispose();
            _uploader?.Dispose();
        }
    }
}
