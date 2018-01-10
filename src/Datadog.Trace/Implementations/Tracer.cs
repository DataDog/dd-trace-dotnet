using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    public class Tracer : IDatadogTracer
    {
        private static readonly ILog _log = LogProvider.For<Tracer>();

        private AsyncLocalScopeManager _scopeManager;
        private string _defaultServiceName;
        private Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private IAgentWriter _agentWriter;
        private bool _isDebugEnabled;

        internal Tracer(IAgentWriter agentWriter, List<ServiceInfo> serviceInfo = null, string defaultServiceName = null, bool isDebugEnabled = false)
        {
            _isDebugEnabled = isDebugEnabled;
            _agentWriter = agentWriter;
            _defaultServiceName = defaultServiceName ?? GetAppDomainFriendlyName() ?? Constants.UnkownService;
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

            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Console.CancelKeyPress += Console_CancelKeyPress;
            _scopeManager = new AsyncLocalScopeManager();
        }

        public Scope ActiveScope => _scopeManager.Active;

        bool IDatadogTracer.IsDebugEnabled => _isDebugEnabled;

        string IDatadogTracer.DefaultServiceName => _defaultServiceName;

        AsyncLocalScopeManager IDatadogTracer.ScopeManager => _scopeManager;

        public Scope ActivateSpan(Span span, bool finishOnClose = true)
        {
            return _scopeManager.Activate(span, finishOnClose);
        }

        public Scope StartActive(string operationName, SpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false, bool finishOnClose = true)
        {
            var span = StartManual(operationName, parent, serviceName, startTime, ignoreActiveScope);
            return _scopeManager.Activate(span, finishOnClose);
        }

        public Span StartManual(string operationName, SpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool ignoreActiveScope = false)
        {
            if (parent == null && !ignoreActiveScope)
            {
                parent = _scopeManager.Active?.Span?.Context;
            }

            var span = new Span(this, parent, operationName, serviceName, startTime);
            span.TraceContext.AddSpan(span);
            return span;
        }

        void IDatadogTracer.Write(List<Span> trace)
        {
            _agentWriter.WriteTrace(trace);
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