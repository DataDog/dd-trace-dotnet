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
            ConsoleWrite.LineLine($"Settng up {this.GetType().Name} listening.");
            SubscribeToAllSources();
            ConsoleWrite.LineLine($"Finished settng up {this.GetType().Name} listening.");
        }

        private void OnAllEventSourcesSubscriptionCompleted()
        {
            ConsoleWrite.LineLine($"All-EventSources-Subscription Completed. Scheduling a re-subscription.");
            Task.Run(async () =>
                {
                    await Task.Delay(100);

                    ConsoleWrite.LineLine($"Renewing top-level {this.GetType().Name} subscriptions.");
                    SubscribeToAllSources();
                    ConsoleWrite.LineLine($"Finished renewing top-level {this.GetType().Name} subscriptions.");
                });
        }

        private IDisposable SubscribeToAllSources()
        {
            try
            {
                return DiagnosticListening.SubscribeToAllSources(ObserverAdapter.OnAllHandlers(
                            (DiagnosticListenerStub dl) => OnEventSourceObservered(dl),
                            null,
                            () => OnAllEventSourcesSubscriptionCompleted()));
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
                return null;
            }
        }

        private void OnEventSourceObservered(DiagnosticListenerStub diagnosticListener)
        {
            string diagnosticListenerName = GetName(diagnosticListener);
            if (diagnosticListenerName == null)
            {
                return;
            }

            if (diagnosticListenerName.Equals(DiagnosticEventsSpecification.DirectSourceName, StringComparison.Ordinal))
            {
                SubscribeToEvents(diagnosticListener, DiagnosticEventsSpecification.DirectSourceEventName, OnEventObservered);
            }

            if (diagnosticListenerName.Equals(DiagnosticEventsSpecification.StubbedSourceName, StringComparison.Ordinal))
            {
                SubscribeToEvents(diagnosticListener, DiagnosticEventsSpecification.StubbedSourceEventName, OnEventObservered);
            }
        }

        private string GetName(DiagnosticListenerStub diagnosticListener)
        {
            try
            {
                return diagnosticListener.Name;
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
                return null;
            }
        }

        private IDisposable SubscribeToEvents(DiagnosticListenerStub diagnosticListener, string eventNamePrefix, Action<KeyValuePair<string, object>> eventHandler)
        {
            try
            {
                if (eventNamePrefix == null)
                {
                    return diagnosticListener.SubscribeToEvents(ObserverAdapter.OnNextHandler(eventHandler), null);
                }
                else
                {
                    return diagnosticListener.SubscribeToEvents(
                            ObserverAdapter.OnNextHandler(eventHandler),
                            (string eventName, object _, object __) => (eventName != null) && eventName.StartsWith(eventNamePrefix, StringComparison.Ordinal));
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.Exception(ex);
                return null;
            }
        }

        private void OnEventObservered(KeyValuePair<string, object> eventInfo)
        {
            if (eventInfo.Key != null
                    && eventInfo.Value != null
                    && eventInfo.Value is DiagnosticEventsSpecification.EventPayload eventPayload)
            {
                if (eventInfo.Key.Equals(DiagnosticEventsSpecification.DirectSourceEventName, StringComparison.Ordinal)
                        && eventPayload.SourceName != null
                        && eventPayload.SourceName.Equals(DiagnosticEventsSpecification.DirectSourceName, StringComparison.Ordinal))
                {
                    _directSourceResultAccumulator.SetReceived(eventPayload.Iteration);
                    return;
                }

                if (eventInfo.Key.Equals(DiagnosticEventsSpecification.StubbedSourceEventName, StringComparison.Ordinal)
                        && eventPayload.SourceName != null
                        && eventPayload.SourceName.Equals(DiagnosticEventsSpecification.StubbedSourceName, StringComparison.Ordinal))
                {
                    _stubbedSourceResultAccumulator.SetReceived(eventPayload.Iteration);
                    return;
                }
            }

            ConsoleWrite.Line();
            ConsoleWrite.Line($"Unexpected event info:");
            ConsoleWrite.Line($"    Name:          \"{eventInfo.Key ?? "<null>"}\"");
            ConsoleWrite.Line($"    Payload type:  \"{eventInfo.Value?.GetType()?.FullName ?? "<null>"}\"");
            ConsoleWrite.Line($"    Payload value: \"{eventInfo.Value?.ToString() ?? "<null>"}\"");
        }
    }
}
