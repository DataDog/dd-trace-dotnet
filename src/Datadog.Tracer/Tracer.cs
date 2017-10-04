using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Tracer
{
    public class Tracer : ITracer, IDatadogTracer
    {
        private AsyncLocal<TraceContext> _currentContext = new AsyncLocal<TraceContext>();
        private string _defaultServiceName;
        private bool _automaticContextPropagation;

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        public Tracer(string defaultServiceName = Constants.UnkownService, bool automaticContextPropagation = true)
        {
            //TODO:bertrand be smarter about the service name
            _defaultServiceName = defaultServiceName;
            _automaticContextPropagation = true;
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
            if (!_automaticContextPropagation)
            {
                return new TraceContext(this);
            }
            if(_currentContext.Value == null)
            {
                _currentContext.Value = new TraceContext(this);
            }
            return _currentContext.Value;
        }
    }
}
