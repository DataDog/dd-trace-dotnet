using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.OpenTracing;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace
{
    internal class OpenTracingTracer : ITracer
    {
        private static readonly ILog _log = LogProvider.For<OpenTracingTracer>();

        private readonly global::OpenTracing.IScopeManager _scopeManager;
        private readonly IDatadogTracer _tracer;
        private readonly Dictionary<string, ICodec> _codecs;

        public OpenTracingTracer(IDatadogTracer tracer)
        {
            _tracer = tracer;
            _codecs = new Dictionary<string, ICodec> { { BuiltinFormats.HttpHeaders.ToString(), new HttpHeadersCodec() } };

            IScopeManager ddScopeManager = ((IDatadogTracer)tracer).ScopeManager;
            _scopeManager = new OpenTracingScopeManager(ddScopeManager);
        }

        /*
        public OpenTracingTracer(global::OpenTracing.IScopeManager scopeManager, IAgentWriter agentWriter, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            _scopeManager = scopeManager;
            _tracer = new Tracer(agentWriter, defaultServiceName, isDebugEnabled);
            _codecs = new Dictionary<string, ICodec> { { BuiltinFormats.HttpHeaders.ToString(), new HttpHeadersCodec() } };
        }
        */

        public global::OpenTracing.IScopeManager ScopeManager => _scopeManager;

        public ISpan ActiveSpan => _scopeManager?.Active?.Span;

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new OpenTracingSpanBuilder(_tracer, operationName);
        }

        public ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            string formatKey = format.ToString();
            _codecs.TryGetValue(formatKey, out ICodec codec);

            if (codec != null)
            {
                return codec.Extract(carrier);
            }
            else
            {
                _log.Error($"Tracer.Extract is not implemented for {formatKey} by Datadog.Trace");
                throw new NotSupportedException();
            }
        }

        public void Inject<TCarrier>(ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier)
        {
            string formatKey = format.ToString();
            _codecs.TryGetValue(formatKey, out ICodec codec);

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
                _log.Error($"Tracer.Inject is not implemented for {formatKey} by Datadog.Trace");
                throw new NotSupportedException();
            }
        }
    }
}