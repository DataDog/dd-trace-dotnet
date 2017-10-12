using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Datadog.Tracer
{
    internal class Tracer : ITracer, IDatadogTracer, IObservable<List<Span>>, IObservable<ServiceInfo>
    {
        private AsyncLocal<TraceContext> _currentContext = new AsyncLocal<TraceContext>();
        private string _defaultServiceName;
        private Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private ISubject<List<Span>> _traceSubject = Subject.Synchronize(new Subject<List<Span>>());
        private IObservable<ServiceInfo> _serviceSubject;

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        public Tracer(List<ServiceInfo> serviceInfo = null, string defaultServiceName = Constants.UnkownService)
        {
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
            _serviceSubject = _services.Values.ToObservable();
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
            _traceSubject.OnNext(trace);
        }

        ITraceContext IDatadogTracer.GetTraceContext()
        {
            if(_currentContext.Value == null)
            {
                _currentContext.Value = new TraceContext(this);
            }
            return _currentContext.Value;
        }

        public IDisposable Subscribe(IObserver<List<Span>> observer)
        {
            return _traceSubject.Subscribe(observer);
        }

        public IDisposable Subscribe(IObserver<ServiceInfo> observer)
        {
            return _serviceSubject.Subscribe(observer);
        }
    }
}
