using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using OpenTracing;
using OpenTracing.Propagation;

namespace Datadog.Trace
{
    internal class Tracer : ITracer, IDatadogTracer
    {
        private static readonly ILog _log = LogProvider.For<Tracer>();

        private AsyncLocalCompat<TraceContext> _currentContext = new AsyncLocalCompat<TraceContext>("Datadog.Trace.Tracer._currentContext");
        private string _defaultServiceName;
        private Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private IAgentWriter _agentWriter;
        private bool _isDebugEnabled;
        private Dictionary<string, ICodec> _codecs;

        public Tracer(IAgentWriter agentWriter, List<ServiceInfo> serviceInfo = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            _isDebugEnabled = isDebugEnabled;
            _agentWriter = agentWriter;
            _defaultServiceName = GetAppDomainFriendlyName() ?? Constants.UnkownService;
            if (serviceInfo != null)
            {
                foreach (var service in serviceInfo)
                {
                    _services[service.ServiceName] = service;
                }
            }

            foreach (var service in _services.Values)
            {
                _agentWriter.WriteServiceInfo(service);
            }

            _codecs = new Dictionary<string, ICodec> { { Formats.HttpHeaders.Name, new HttpHeadersCodec(this) } };

            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Console.CancelKeyPress += Console_CancelKeyPress;
        }

        bool IDatadogTracer.IsDebugEnabled => _isDebugEnabled;

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new SpanBuilder(this, operationName);
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
                _log.Error($"Tracer.Extract is not implemented for {format.Name} by Datadog.Trace");
                throw new UnsupportedFormatException();
            }
        }

        public void Inject<TCarrier>(ISpanContext spanContext, Format<TCarrier> format, TCarrier carrier)
        {
            _codecs.TryGetValue(format.Name, out ICodec codec);
            if (codec != null)
            {
                var ddSpanContext = spanContext as SpanContext;
                if (ddSpanContext == null)
                {
                    throw new ArgumentException("Inject should be called with a Datadog.Trace.SpanContext argument");
                }

                codec.Inject(ddSpanContext, carrier);
            }
            else
            {
                _log.Error($"Tracer.Inject is not implemented for {format.Name} by Datadog.Trace");
                throw new UnsupportedFormatException();
            }
        }

        void IDatadogTracer.Write(List<SpanBase> trace)
        {
            _agentWriter.WriteTrace(trace);
        }

        ITraceContext IDatadogTracer.GetTraceContext()
        {
            if (_currentContext.Get() == null)
            {
                _currentContext.Set(new TraceContext(this));
            }

            return _currentContext.Get();
        }

        void IDatadogTracer.CloseCurrentTraceContext()
        {
            _currentContext.Set(null);
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _agentWriter.FlushAndCloseAsync().Wait();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _agentWriter.FlushAndCloseAsync().Wait();
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _agentWriter.FlushAndCloseAsync().Wait();
        }

        private string GetAppDomainFriendlyName()
        {
            try
            {
                return AppDomain.CurrentDomain.FriendlyName;
            }
            catch
            {
                return null;
            }
        }
    }
}
