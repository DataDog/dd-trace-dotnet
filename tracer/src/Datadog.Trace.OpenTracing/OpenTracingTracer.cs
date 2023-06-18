// <copyright file="OpenTracingTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingTracer : global::OpenTracing.ITracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OpenTracingTracer>();

        private readonly Dictionary<string, ICodec> _codecs;

        [PublicApi]
        public OpenTracingTracer(IDatadogOpenTracingTracer datadogTracer)
            : this(datadogTracer, CreateDefaultScopeManager(), datadogTracer.DefaultServiceName)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.OpenTracingTracer_Ctor_DatadogTracer);
        }

        [PublicApi]
        public OpenTracingTracer(IDatadogOpenTracingTracer datadogTracer, global::OpenTracing.IScopeManager scopeManager)
            : this(datadogTracer, scopeManager, datadogTracer.DefaultServiceName)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.OpenTracingTracer_Ctor_DatadogTracer_ScopeManager);
        }

        internal OpenTracingTracer(
            IDatadogOpenTracingTracer datadogTracer,
            global::OpenTracing.IScopeManager scopeManager,
            string defaultServiceName)
        {
            DatadogTracer = datadogTracer;
            DefaultServiceName = defaultServiceName;
            ScopeManager = scopeManager;
            _codecs = new Dictionary<string, ICodec>
            {
                { BuiltinFormats.HttpHeaders.ToString(), new HttpHeadersCodec() },
                { BuiltinFormats.TextMap.ToString(), new HttpHeadersCodec() } // the HttpHeadersCodec can support an unconstrained ITextMap
            };
        }

        public IDatadogOpenTracingTracer DatadogTracer { get; }

        public string DefaultServiceName { get; }

        public global::OpenTracing.IScopeManager ScopeManager { get; }

        public OpenTracingSpan ActiveSpan => (OpenTracingSpan)ScopeManager.Active?.Span;

        global::OpenTracing.ISpan global::OpenTracing.ITracer.ActiveSpan => ScopeManager.Active?.Span;

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new OpenTracingSpanBuilder(this, operationName);
        }

        public global::OpenTracing.ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.ToString(), out ICodec codec);

            if (codec != null)
            {
                return codec.Extract(carrier);
            }

            throw new NotSupportedException($"Tracer.Extract is not implemented for {format} by Datadog.Trace");
        }

        public void Inject<TCarrier>(global::OpenTracing.ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.ToString(), out ICodec codec);

            if (codec != null)
            {
                codec.Inject(spanContext, carrier);
            }
            else
            {
                throw new NotSupportedException($"Tracer.Inject is not implemented for {format} by Datadog.Trace");
            }
        }

        internal static global::OpenTracing.IScopeManager CreateDefaultScopeManager()
            => new global::OpenTracing.Util.AsyncLocalScopeManager();
    }
}
