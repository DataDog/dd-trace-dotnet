using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DiagnosticListeners
{
    internal abstract class DiagnosticObserver : IObserver<KeyValuePair<string, object>>
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<DiagnosticObserver>();

        protected DiagnosticObserver(IDatadogTracer tracer)
        {
            Tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        protected IDatadogTracer Tracer { get; }

        public virtual bool IsSubscriberEnabled()
        {
            return true;
        }

        public virtual IDisposable SubscribeIfMatch(DiagnosticListener diagnosticListener)
        {
            if (diagnosticListener.Name == GetListenerName())
            {
                return diagnosticListener.Subscribe(this, IsEventEnabled);
            }

            return null;
        }

        void IObserver<KeyValuePair<string, object>>.OnCompleted()
        {
        }

        void IObserver<KeyValuePair<string, object>>.OnError(Exception error)
        {
        }

        void IObserver<KeyValuePair<string, object>>.OnNext(KeyValuePair<string, object> value)
        {
            try
            {
                OnNext(value.Key, value.Value);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Event Exception: {0}", value.Key);
            }
        }

        /// <summary>
        /// Gets the name of the <see cref="DiagnosticListener"/> that should be instrumented.
        /// </summary>
        /// <returns>The name of the <see cref="DiagnosticListener"/> that should be instrumented.</returns>
        protected abstract string GetListenerName();

        protected virtual bool IsEventEnabled(string eventName)
        {
            return true;
        }

        protected abstract void OnNext(string eventName, object arg);
    }
}
