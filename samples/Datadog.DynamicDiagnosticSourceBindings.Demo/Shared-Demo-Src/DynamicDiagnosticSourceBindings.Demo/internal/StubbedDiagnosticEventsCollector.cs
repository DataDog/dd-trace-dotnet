using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.DynamicDiagnosticSourceBindings;
using Datadog.Util;

namespace DynamicDiagnosticSourceBindings.Demo
{
    internal class StubbedDiagnosticEventsCollector
    {
        private readonly ReceivedEventsAccumulator _directSourceResultAccumulator;
        private readonly ReceivedEventsAccumulator _stubbedSourceResultAccumulator;

        public StubbedDiagnosticEventsCollector(ReceivedEventsAccumulator directSourceResultAccumulator, ReceivedEventsAccumulator stubbedSourceResultAccumulator)
        {
            _directSourceResultAccumulator = directSourceResultAccumulator;
            _stubbedSourceResultAccumulator = stubbedSourceResultAccumulator;

            SetupListening();
        }

        private void SetupListening()
        {
            Console.WriteLine();
            Console.WriteLine($"Settng up {this.GetType().Name} listening.");

            StatefulObserverAdapter<DiagnosticListenerStub, IDisposable> allDiagnosticSourcesObserver = ObserverAdapter.OnAllHandlers(
                    (DiagnosticListenerStub dl, IDisposable _) => OnEventSourceObservered(dl, _),
                    (Exception err, IDisposable _) => Console.WriteLine($"Error passed to the All-EventSources-Observer (this should never happen): {err?.ToString() ?? "<null>"}"),
                    (IDisposable allDsSub) => OnAllEventSourcesSubscriptionCompleted(allDsSub));

            IDisposable allDiagnosticSourcesSubscription = DiagnosticListening.SubscribeToAllSources(allDiagnosticSourcesObserver);
            allDiagnosticSourcesObserver.State = allDiagnosticSourcesSubscription;

            Console.WriteLine();
            Console.WriteLine($"Finished settng up {this.GetType().Name} listening.");
        }

        private void OnEventSourceObservered(DiagnosticListenerStub diagnosticListener, IDisposable ____)
        {
            if (diagnosticListener.Name.Equals(DiagnosticEventsSpecification.DirectSourceName, StringComparison.Ordinal))
            {
                StatefulObserverAdapter<KeyValuePair<string, object>, IDisposable> eventsObserver = ObserverAdapter.OnAllHandlers(
                        (KeyValuePair<string, object> eventInfo, IDisposable ___) => OnEventObservered(eventInfo),
                        null,
                        (IDisposable evntsSub) => evntsSub?.Dispose());

                IDisposable eventSubscription = diagnosticListener.SubscribeToEvents(
                        eventsObserver,
                        (string eventName, object __, object _) =>
                                (eventName != null) && eventName.StartsWith(DiagnosticEventsSpecification.DirectSourceEventName, StringComparison.Ordinal));

                eventsObserver.State = eventSubscription;
            }

            if (diagnosticListener.Name.Equals(DiagnosticEventsSpecification.StubbedSourceName, StringComparison.Ordinal))
            {
                StatefulObserverAdapter<KeyValuePair<string, object>, IDisposable> eventsObserver = ObserverAdapter.OnAllHandlers(
                        (KeyValuePair<string, object> eventInfo, IDisposable ___) => OnEventObservered(eventInfo),
                        null,
                        (IDisposable evntsSub) => evntsSub?.Dispose());

                IDisposable eventSubscription = diagnosticListener.SubscribeToEvents(
                        eventsObserver,
                        (string eventName, object __, object _) =>
                                (eventName != null) && eventName.StartsWith(DiagnosticEventsSpecification.StubbedSourceEventName, StringComparison.Ordinal));

                eventsObserver.State = eventSubscription;
            }
        }

        private void OnAllEventSourcesSubscriptionCompleted(IDisposable completedAllDiagnosticSourcesSubscription)
        {
            Console.WriteLine();
            Console.WriteLine("All-EventSources-Subscription Completed. Scheduling a re-subscription.");

            if (completedAllDiagnosticSourcesSubscription != null)
            {
                completedAllDiagnosticSourcesSubscription.Dispose();
            }

            Task.Run(async () =>
            {
                await Task.Delay(100);

                Console.WriteLine($"Renewing top-level {this.GetType().Name} subscriptions.");

                StatefulObserverAdapter<DiagnosticListenerStub, IDisposable> newAllDiagnosticSourcesObserver = ObserverAdapter.OnAllHandlers(
                    (DiagnosticListenerStub dl, IDisposable _) => OnEventSourceObservered(dl, _),
                    (Exception err, IDisposable _) => Console.WriteLine($"Error passed to the All-EventSources-Observer (this should never happen): {err?.ToString() ?? "<null>"}"),
                    (IDisposable allDsSub) => OnAllEventSourcesSubscriptionCompleted(allDsSub));

                IDisposable newAllDiagnosticSourcesSubscription = DiagnosticListening.SubscribeToAllSources(newAllDiagnosticSourcesObserver);
                newAllDiagnosticSourcesObserver.State = newAllDiagnosticSourcesSubscription;

                Console.WriteLine();
                Console.WriteLine($"Finished renewing top-level {this.GetType().Name} subscriptions.");
            });
        }

        private void OnEventObservered(KeyValuePair<string, object> eventInfo)
        {
            if (eventInfo.Value == null || ! (eventInfo.Value is DiagnosticEventsSpecification.EventPayload eventPayload))
            {
                Console.WriteLine($"{Environment.NewLine}Unexpected event payload type: {eventInfo.Value?.GetType()?.FullName ?? "<null>"}");
                return;
            }

            if (eventPayload.SourceName.Equals(DiagnosticEventsSpecification.DirectSourceName, StringComparison.Ordinal))
            {
                _directSourceResultAccumulator.SetReceived(eventPayload.Iteration);
            }
            else if (eventPayload.SourceName.Equals(DiagnosticEventsSpecification.StubbedSourceName, StringComparison.Ordinal))
            {
                _stubbedSourceResultAccumulator.SetReceived(eventPayload.Iteration);
            }
            else
            {
                Console.WriteLine($"{Environment.NewLine}Unexpected source name in an event: {eventPayload.ToString()}");
            }
        }
    }
}
