using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingTracer : ITracer
    {
        private static readonly ILog _log = LogProvider.For<OpenTracingTracer>();

        private readonly Dictionary<string, ICodec> _codecs;

        public OpenTracingTracer(IDatadogTracer datadogTracer)
            : this(datadogTracer, new global::OpenTracing.Util.AsyncLocalScopeManager())
        {
        }

        public OpenTracingTracer(IDatadogTracer datadogTracer, IScopeManager scopeManager)
        {
            DatadogTracer = datadogTracer;
            DefaultServiceName = datadogTracer.DefaultServiceName;
            ScopeManager = scopeManager;
            _codecs = new Dictionary<string, ICodec> { { BuiltinFormats.HttpHeaders.ToString(), new HttpHeadersCodec() } };
        }

        public IDatadogTracer DatadogTracer { get; }

        public string DefaultServiceName { get; }

        public IScopeManager ScopeManager { get; }

        public OpenTracingSpan ActiveSpan => (OpenTracingSpan)ScopeManager.Active?.Span;

        ISpan ITracer.ActiveSpan => ScopeManager.Active?.Span;

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new OpenTracingSpanBuilder(this, operationName);
        }

        public ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.ToString(), out ICodec codec);
            if (codec != null)
            {
                return codec.Extract(carrier);
            }
            else
            {
                throw new NotSupportedException($"Tracer.Extract is not implemented for {format} by Datadog.Trace");
            }
        }

        public void Inject<TCarrier>(ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.ToString(), out ICodec codec);
            if (codec != null)
            {
                var ddSpanContext = spanContext as OpenTracingSpanContext;
                if (ddSpanContext == null)
                {
                    throw new ArgumentException("Inject should be called with a Datadog.Trace.SpanContext argument");
                }

                codec.Inject(ddSpanContext, carrier);
            }
            else
            {
                throw new NotSupportedException($"Tracer.Inject is not implemented for {format} by Datadog.Trace");
            }
        }
    }
}
