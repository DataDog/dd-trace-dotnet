using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Tracer
{
    internal class Tracer : ITracer, IDatadogTracer
    {
        private AsyncLocal<TraceContext> _currentContext = new AsyncLocal<TraceContext>();
        private string _defaultServiceName;
        private IApi _api;
        private Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        public Tracer(IApi api, List<ServiceInfo> serviceInfo = null, string defaultServiceName = Constants.UnkownService)
        {
            _api = api;
            // TODO:bertrand be smarter about the service name
            _defaultServiceName = defaultServiceName;
            if (defaultServiceName == Constants.UnkownService)
            {
                _services[Constants.UnkownService] = new ServiceInfo
                {
                    ServiceName = Constants.UnkownService,
                    App = Constants.UnkownApp,
                    AppType = Constants.WebAppType,
                };
            }
            if (serviceInfo != null)
            {
                foreach(var service in serviceInfo)
                {
                    _services[service.ServiceName] = service;
                }
            }
            // TODO:bertrand handle errors properly
            Task.WhenAll(_services.Values.Select(_api.SendServiceAsync)).Wait();
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
        void IDatadogTracer.Write(List<Span> trace)
        {
            // TODO:bertrand should be non blocking + retry mechanism
            _api.SendTracesAsync(new List<List<Span>> { trace }).Wait();
        }

        ITraceContext IDatadogTracer.GetTraceContext()
        {
            if(_currentContext.Value == null)
            {
                _currentContext.Value = new TraceContext(this);
            }
            return _currentContext.Value;
        }
    }
}
