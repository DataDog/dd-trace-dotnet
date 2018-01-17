using System;
using System.Diagnostics;

namespace Datadog.Trace.AspNetCore
{
    internal abstract class ListenerBase : IObserver<DiagnosticListener>, IDisposable
    {
        private IDisposable _subscription;

        protected abstract string ListenerName { get; set; }

        public void Subscribe()
        {
            _subscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == ListenerName)
            {
                // value.SubscribeWithAdapter(this);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            if (_subscription != null)
            {
                _subscription.Dispose();
            }
        }
    }
}
