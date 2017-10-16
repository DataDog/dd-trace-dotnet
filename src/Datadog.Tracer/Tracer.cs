using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Tracer
{
    internal class Tracer : ITracer, IDatadogTracer
    {
        private AsyncLocal<TraceContext> _currentContext = new AsyncLocal<TraceContext>();
        private string _defaultServiceName;
        private Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private IAgentWriter _agentWriter;

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        public Tracer(IAgentWriter agentWriter, List<ServiceInfo> serviceInfo = null, string defaultServiceName = null)
        {
            _agentWriter = agentWriter;
            // TODO:bertrand be smarter about the service name
            _defaultServiceName = defaultServiceName ?? Constants.UnkownService;
            if (_defaultServiceName == Constants.UnkownService)
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
            foreach(var service in _services.Values)
            {
                _agentWriter.WriteServiceInfo(service);
            }
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

        void IDatadogTracer.Write(List<Span> trace)
        {
            // TODO:bertrand send me
            _agentWriter.WriteTrace(trace);
        }

        ITraceContext IDatadogTracer.GetTraceContext()
        {
            if(_currentContext.Value == null)
            {
                _currentContext.Value = new TraceContext(this);
            }
            return _currentContext.Value;
        }

        void IDatadogTracer.CloseCurrentTraceContext()
        {
            _currentContext.Value = null;
        }
    }
}
