using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    public class Tracer : ITracer, IDatadogTracer
    {
        private string _defaultServiceName;

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        public Tracer(string defaultServiceName = "UnknownService")
        {
            //TODO:bertrand be smarter about the service name
            _defaultServiceName = defaultServiceName;
        }

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new SpanBuilder(this, operationName);
        }

        public ISpanContext Extract<TCarrier>(Format<TCarrier> format, TCarrier carrier)
        {
            throw new NotImplementedException();
        }

        public void Inject<TCarrier>(ISpanContext spanContext, Format<TCarrier> format, TCarrier carrier)
        {
            throw new NotImplementedException();
        }


        // Trick to keep the method from being accessed from outside the assembly while having it exposed as an interface.
        // https://stackoverflow.com/a/18944374
        void IDatadogTracer.Write(List<Span> span)
        {
            throw new NotImplementedException();
        }

        ITraceContext IDatadogTracer.GetTraceContext()
        {
            return new TraceContext(this);
        }
    }
}
