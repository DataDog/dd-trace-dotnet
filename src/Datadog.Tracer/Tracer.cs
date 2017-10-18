using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections.Generic;
using System.Reflection;
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
            _defaultServiceName = defaultServiceName;
            if (_defaultServiceName == null)
            {
                _defaultServiceName = GetExecutingAssemblyName() ?? Constants.UnkownService;
                _services[_defaultServiceName] = new ServiceInfo
                {
                    ServiceName = _defaultServiceName,
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

        private string GetExecutingAssemblyName()
        {
            try
            {
                var name = Assembly.GetExecutingAssembly().GetName();
                return name.Name;
            }
            catch
            {
                return null;
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
