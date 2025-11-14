// <copyright file="DiagnosticManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

#pragma warning disable SA1402
namespace Datadog.Trace.DiagnosticListeners
{
    internal sealed class DiagnosticManager : IDiagnosticManager, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticManager>();

        private readonly IEnumerable<DiagnosticObserver> _diagnosticObservers;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private IDisposable? _allListenersSubscription;

        public DiagnosticManager(IEnumerable<DiagnosticObserver> diagnosticSubscribers)
        {
            if (diagnosticSubscribers == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(diagnosticSubscribers));
            }

            _diagnosticObservers = diagnosticSubscribers;
        }

        public static DiagnosticManager? Instance { get; set; }

        public bool IsRunning => _allListenersSubscription != null;

        public void Start()
        {
            if (_allListenersSubscription == null)
            {
                Log.Debug("Starting DiagnosticListener.AllListeners subscription");
#if !NETFRAMEWORK
                _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(new DiagnosticListenerObserver(this));
#else
                try
                {
                    // Get the DiagnosticListener type
                    var diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource");
                    if (diagnosticListenerType == null)
                    {
                        Log.Warning("Unable to find DiagnosticListener type");
                        return;
                    }

                    _allListenersSubscription = DiagnosticObserverFactory.SubscribeWithInstanceCallback(
                        diagnosticListenerType,
                        this,
                        typeof(DiagnosticManager),
                        nameof(OnDiagnosticListenerNext),
                        "Datadog.DiagnosticManager.Dynamic",
                        typeof(DiagnosticManager));

                    if (_allListenersSubscription != null)
                    {
                        Log.Debug("Successfully subscribed to DiagnosticListener.AllListeners");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error starting DiagnosticListener.AllListeners subscription");
                }
#endif
            }
        }

        public void OnNext(IDiagnosticListener listener)
        {
            foreach (var subscriber in _diagnosticObservers)
            {
                if (!subscriber.IsSubscriberEnabled())
                {
                    continue;
                }

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

        /// <summary>
        /// Called when a new DiagnosticListener is created.
        /// This method is called by the dynamically created observer type via reflection.
        /// </summary>
        /// <param name="diagnosticListener">The DiagnosticListener instance (actual System.Diagnostics type)</param>
        internal void OnDiagnosticListenerNext(object diagnosticListener)
        {
            try
            {
                // Duck type the actual DiagnosticListener to our interface
                var listener = diagnosticListener.DuckAs<IDiagnosticListener>();
                if (listener?.Instance != null)
                {
                    OnNext(listener);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticListener notification");
            }
        }
    }

#if !NETFRAMEWORK
    internal sealed class DiagnosticListenerObserver : IObserver<DiagnosticListener>
    {
        private DiagnosticManager _diagnosticManager;

        public DiagnosticListenerObserver(DiagnosticManager diagnosticManager)
        {
            _diagnosticManager = diagnosticManager;
        }

        public void OnCompleted()
        {
            return;
        }

        public void OnError(Exception error)
        {
            return;
        }

        public void OnNext(DiagnosticListener value)
        {
            _diagnosticManager.OnDiagnosticListenerNext(value);
        }
    }
#endif
}
