using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.SqlClient
{
    internal class GlobalListener : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly string _sourceName;
        private readonly object _target;
        private List<IDisposable> _subscriptions = new List<IDisposable>();

        public GlobalListener(string sourceName, object target)
        {
            _sourceName = sourceName;
            _target = target;
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener diagnosticListener)
        {
            if (diagnosticListener.Name == _sourceName)
            {
                _subscriptions.Add(diagnosticListener.SubscribeWithAdapter(_target));
            }
        }

        void IObserver<DiagnosticListener>.OnCompleted()
        {
        }

        void IObserver<DiagnosticListener>.OnError(Exception error)
        {
        }
    }
}