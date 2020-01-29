using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.DiagnosticListeners
{
    internal sealed class DiagnosticManager : IDiagnosticManager, IObserver<DiagnosticListener>, IDisposable
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<DiagnosticManager>();

        private readonly IEnumerable<DiagnosticObserver> _diagnosticObservers;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private IDisposable _allListenersSubscription;

        public DiagnosticManager(IEnumerable<DiagnosticObserver> diagnosticSubscribers)
        {
            if (diagnosticSubscribers == null)
            {
                throw new ArgumentNullException(nameof(diagnosticSubscribers));
            }

            _diagnosticObservers = diagnosticSubscribers.Where(x => x.IsSubscriberEnabled());
        }

        public bool IsRunning => _allListenersSubscription != null;

        public void Start()
        {
            if (_allListenersSubscription == null)
            {
                Log.Verbose("Starting AllListeners subscription");
                _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        void IObserver<DiagnosticListener>.OnCompleted()
        {
        }

        void IObserver<DiagnosticListener>.OnError(Exception error)
        {
        }

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
        {
            foreach (var subscriber in _diagnosticObservers)
            {
                IDisposable subscription = subscriber.SubscribeIfMatch(listener);

                if (subscription != null)
                {
                    if (Log.IsEnabled(LogEventLevel.Verbose))
                    {
                        Log.Verbose(
                            "Subscriber '{0}' returned subscription for '{1}'",
                            subscriber.GetType().Name,
                            listener.Name);
                    }

                    _subscriptions.Add(subscription);
                }
            }
        }

        public void Stop()
        {
            if (_allListenersSubscription != null)
            {
                if (Log.IsEnabled(LogEventLevel.Verbose))
                {
                    Log.Verbose("Stopping AllListeners subscription");
                }

                _allListenersSubscription.Dispose();
                _allListenersSubscription = null;

                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
