// <copyright file="ExceptionReplay.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionReplay : IDisposable
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionReplay));
        private bool _isDisabled;
        private SnapshotUploader? _uploader;
        private SnapshotSink? _snapshotSink;
        private ExceptionTrackManager? _exceptionTrackManager;

        private ExceptionReplay(ExceptionReplaySettings settings)
        {
            Settings = settings;
        }

        internal ExceptionReplaySettings Settings { get; }

        internal bool Enabled
        {
            get => Settings.Enabled && !_isDisabled;
        }

        internal static ExceptionReplay Create(ExceptionReplaySettings settings)
        {
            return new ExceptionReplay(settings);
        }

        public void Initialize()
        {
            if (!Enabled)
            {
                Log.Information("Exception replay is disabled.");
                return;
            }

            Log.Information("Initializing Exception Debugging");

            if (!ThirdPartyModules.IsValid)
            {
                Log.Warning("Third party modules load has failed. Disabling Exception Debugging.");
                _isDisabled = true;
                return;
            }

            InitSnapshotsSink();
            _exceptionTrackManager = ExceptionTrackManager.Create(Settings);
            return;
        }

        private void InitSnapshotsSink()
        {
            var tracer = Tracer.Instance;
            var debuggerSettings = DebuggerSettings.FromDefaultSource();

            // Set configs relevant for DI and Exception Debugging, using DI's environment keys.
            DebuggerSnapshotSerializer.SetConfig(debuggerSettings);
            Redaction.Instance.SetConfig(debuggerSettings.RedactedIdentifiers, debuggerSettings.RedactedExcludedIdentifiers, debuggerSettings.RedactedTypes);

            // Set up the snapshots sink.
            var snapshotSlicer = SnapshotSlicer.Create(debuggerSettings);
            _snapshotSink = SnapshotSink.Create(debuggerSettings, snapshotSlicer);
            var apiFactory = AgentTransportStrategy.Get(
                tracer.Settings.Exporter,
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

            _ = Task.Run(() => _uploader.StartFlushingAsync())
                    .ContinueWith(
                         t =>
                         {
                             if (t.Exception?.GetType() != typeof(OperationCanceledException))
                             {
                                 Log.Error(t.Exception, "Error in flushing task");
                             }
                         },
                         TaskContinuationOptions.OnlyOnFaulted);
        }

        internal void Report(Span span, Exception exception)
        {
            if (!Enabled)
            {
                Log.Debug("Exception replay is disabled.");
                return;
            }

            _exceptionTrackManager?.Report(span, exception);
        }

        internal void BeginRequest()
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

        internal void EndRequest()
        {
            if (!Enabled)
            {
                return;
            }

            ShadowStackHolder.ShadowStack?.Clear();
        }

        internal void AddSnapshot(string probeId, string snapshot)
        {
            if (!Enabled)
            {
                Log.Debug("Exception replay is disabled.");
                return;
            }

            if (_snapshotSink == null)
            {
                Log.Debug("The sink of the Exception Replay is null. Skipping the reporting of the snapshot: {Snapshot}", snapshot);
                return;
            }

            _snapshotSink?.Add(probeId, snapshot);
        }

        public void Dispose()
        {
            SafeDisposal.TryDispose(_exceptionTrackManager);
            SafeDisposal.TryDispose(_uploader);
        }
    }
}
