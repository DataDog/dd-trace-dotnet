// <copyright file="DiagnosticManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticManager>();

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

        public static DiagnosticManager Instance { get; set; }

        public bool IsRunning => _allListenersSubscription != null;

        public void Start()
        {
            if (_allListenersSubscription == null)
            {
                Log.Debug("Starting DiagnosticListener.AllListeners subscription");
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
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug(
                            "Subscriber '{SubscriberType}' returned subscription for '{ListenerName}'",
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
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Stopping DiagnosticListener.AllListeners subscription");
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
#endif
