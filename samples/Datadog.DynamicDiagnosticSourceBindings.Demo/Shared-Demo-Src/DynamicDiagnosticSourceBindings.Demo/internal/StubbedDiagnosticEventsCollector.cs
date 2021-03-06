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

            DiagnosticSourceAssembly.SubscribeDynamicInvokerInitializedListener(OnDynamicDiagnosticSourceInvokerInitialized);
        }

        private void OnDynamicDiagnosticSourceInvokerInitialized(DiagnosticSourceAssembly.IDynamicInvoker dynamicInvoker)
        {
            ConsoleWrite.LineLine($"This {this.GetType().Name} noticed that an"
                                + $" {nameof(DiagnosticSourceAssembly)}.{nameof(DiagnosticSourceAssembly.IDynamicInvoker)} became available."
                                + $" DiagnosticSourceAssemblyName: \"{dynamicInvoker.DiagnosticSourceAssemblyName}\".");

            Guid sessionId = Guid.NewGuid();
            ConsoleWrite.LineLine($"Settng up {this.GetType().Name} listening session \"{sessionId}\".");
            SubscribeToAllSources();
            ConsoleWrite.LineLine($"Finished settng up {this.GetType().Name} listening session \"{sessionId}\".");

            dynamicInvoker.SubscribeInvalidatedListener(OnDynamicDiagnosticSourceInvokerInvalidated, sessionId);
        }

        private void OnDynamicDiagnosticSourceInvokerInvalidated(DiagnosticSourceAssembly.IDynamicInvoker dynamicInvoker, object state)
        {
            // This listener method is just here for demo purposes. It does not perform any business logic (but it could).
            Guid sessionId = (state is Guid stateGuid) ? stateGuid : Guid.Empty;
            ConsoleWrite.LineLine($"This {this.GetType().Name} noticed that a dynamic DiagnosticSource invoker was invalidated (session {sessionId})."
                                + $" Some errors may be temporarily observed until stubs are re-initialized.");
        }

        private IDisposable SubscribeToAllSources()
        {
            try
            {
                return DiagnosticListening.SubscribeToAllSources(ObserverAdapter.OnAllHandlers(
                            (DiagnosticListenerStub dl) => OnEventSourceObservered(dl),
                            (Exception err) => ConsoleWrite.Exception(err),                              // Just for demo. Error handler is not actually necessary.
                            () => ConsoleWrite.LineLine($"All-EventSources-Subscription Completed.")));  // Just for demo. Completion handler is not actually necessary.
            }
            catch (Exception ex)
            {
                // If there was some business logic required to handle such errors, it would go here.
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
                // If there was some business logic required to handle such errors, it would go here.
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
                // If there was some business logic required to handle such errors, it would go here.
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
