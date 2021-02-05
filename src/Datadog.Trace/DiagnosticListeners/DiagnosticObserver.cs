#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DiagnosticListeners
{
    internal abstract class DiagnosticObserver : IObserver<KeyValuePair<string, object>>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticObserver>();

        /// <summary>
        /// Gets the name of the <see cref="DiagnosticListener"/> that should be instrumented.
        /// </summary>
        /// <value>The name of the <see cref="DiagnosticListener"/> that should be instrumented.</value>
        protected abstract string ListenerName { get; }

        public virtual bool IsSubscriberEnabled()
        {
            return true;
        }

        public virtual IDisposable SubscribeIfMatch(DiagnosticListener diagnosticListener)
        {
            if (diagnosticListener.Name == ListenerName)
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
                Log.Error(ex, "Event Exception: {EventName}", value.Key);
#if DEBUG
                // In debug mode we allow exceptions to be catch in the test suite
                throw;
#endif
            }
        }

        protected virtual bool IsEventEnabled(string eventName)
        {
            return true;
        }

        protected abstract void OnNext(string eventName, object arg);
    }
}
#endif
