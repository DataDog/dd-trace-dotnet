using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingTracer : ITracer
    {
        private static readonly ILog _log = LogProvider.For<OpenTracingTracer>();

        private readonly Tracer _tracer;
        private readonly Dictionary<string, ICodec> _codecs;

        public OpenTracingTracer(Tracer tracer)
        {
            _tracer = tracer;
            _codecs = new Dictionary<string, ICodec> { { Formats.HttpHeaders.Name, new HttpHeadersCodec() } };
        }

        public OpenTracingTracer(IAgentWriter agentWriter, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            _tracer = new Tracer(agentWriter, defaultServiceName, isDebugEnabled);
            _codecs = new Dictionary<string, ICodec> { { Formats.HttpHeaders.Name, new HttpHeadersCodec() } };
        }

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new OpenTracingSpanBuilder(_tracer, operationName);
        }

        public ISpanContext Extract<TCarrier>(Format<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.Name, out ICodec codec);
            if (codec != null)
            {
                return codec.Extract(carrier);
            }
            else
            {
                string message = $"Tracer.Extract is not implemented for {format} by Datadog.Trace";
                _log.Error(message);
                throw new NotSupportedException(message);
            }
        }

        public void Inject<TCarrier>(ISpanContext spanContext, Format<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.Name, out ICodec codec);
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
                string message = $"Tracer.Inject is not implemented for {format} by Datadog.Trace";
                _log.Error(message);
                throw new NotSupportedException(message);
            }
        }
    }
}
