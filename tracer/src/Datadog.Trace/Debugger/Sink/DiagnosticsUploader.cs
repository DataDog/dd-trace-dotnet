// <copyright file="DiagnosticsUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Sink
{
    internal class DiagnosticsUploader : DebuggerUploaderBase
    {
        private readonly DiagnosticsSink _diagnosticsSink;
        private readonly IBatchUploader _diagnosticsBatchUploader;

        private DiagnosticsUploader(
            DiagnosticsSink diagnosticsSink,
            IBatchUploader diagnosticsBatchUploader,
            DebuggerSettings settings)
            : base(settings)
        {
            _diagnosticsBatchUploader = diagnosticsBatchUploader;
            _diagnosticsSink = diagnosticsSink;
        }

        public static DiagnosticsUploader Create(DiagnosticsSink diagnosticsSink, IBatchUploader diagnosticsBatchUploader, DebuggerSettings settings)
        {
            return new DiagnosticsUploader(diagnosticsSink, diagnosticsBatchUploader, settings);
        }

        protected override async Task Upload()
        {
            var diagnostics = _diagnosticsSink.GetDiagnostics();
            if (diagnostics.Count > 0)
            {
                await _diagnosticsBatchUploader.Upload(diagnostics.Select(JsonConvert.SerializeObject)).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        protected override int GetRemainingCapacity()
        {
            return _diagnosticsSink.RemainingCapacity();
        }
    }
}
