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
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionDebugging : IDisposable
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionDebugging));
        private bool _isDisabled;

        private SnapshotUploader? _uploader;
        private SnapshotSink? _snapshotSink;
        private ExceptionTrackManager? _exceptionTrackManager;

        private ExceptionDebugging(ExceptionReplaySettings settings)
        {
            Settings = settings;
        }

        internal ExceptionReplaySettings Settings { get; }

        internal static ExceptionDebugging Create(ExceptionReplaySettings settings)
        {
            return new ExceptionDebugging(settings);
        }

        public bool Initialize()
        {
            if (_isDisabled)
            {
                return false;
            }

            Log.Information("Initializing Exception Debugging");

            if (!ThirdPartyModules.IsValid)
            {
                Log.Warning("Third party modules load has failed. Disabling Exception Debugging.");
                _isDisabled = true;
                return false;
            }

            InitSnapshotsSink();
            _exceptionTrackManager = ExceptionTrackManager.Create(Settings);
            return true;
        }

        private void InitSnapshotsSink()
        {
            var tracer = Tracer.Instance;
            var debuggerSettings = DebuggerSettings.FromDefaultSource();

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

            Task.Run(() => _uploader.StartFlushingAsync())
                .ContinueWith(t => Log.Error(t.Exception, "Error in flushing task"), TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Report(Span span, Exception exception)
        {
            if (_isDisabled)
            {
                return;
            }

            _exceptionTrackManager?.Report(span, exception);
        }

        public void BeginRequest()
        {
            if (_isDisabled)
            {
                return;
            }

            var tree = ShadowStackHolder.EnsureShadowStackEnabled();
            tree.Clear();
            tree.Init();
            tree.IsInRequestContext = true;
        }

        public void EndRequest()
        {
            if (_isDisabled)
            {
                return;
            }

            ShadowStackHolder.ShadowStack?.Clear();
        }

        public void AddSnapshot(string probeId, string snapshot)
        {
            if (_isDisabled)
            {
                return;
            }

            if (_snapshotSink == null)
            {
                Log.Debug("The sink of the Exception Debugging is null. Skipping the reporting of the snapshot: {Snapshot}", snapshot);
                return;
            }

            _snapshotSink.Add(probeId, snapshot);
        }

        public void Dispose()
        {
            SafeDisposal.TryDispose(_exceptionTrackManager);
            SafeDisposal.TryDispose(_uploader);
        }
    }
}
