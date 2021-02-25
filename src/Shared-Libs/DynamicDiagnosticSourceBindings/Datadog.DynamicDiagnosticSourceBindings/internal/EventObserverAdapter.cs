using System;
using System.Collections.Generic;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class EventObserverAdapter : IObserver<KeyValuePair<string, object>>
    {
        private readonly Action<string, object> _eventObserver;

        public EventObserverAdapter(Action<string, object> eventObserver)
        {
            _eventObserver = eventObserver;
        }

        public void OnNext(KeyValuePair<string, object> eventInfo)
        {
            Action<string, object> eventObserver = _eventObserver;
            if (eventObserver != null)
            {
                eventObserver(eventInfo.Key, eventInfo.Value);
            }
        }

        public void OnError(Exception error)
        {
            // This should never be invoked in practice. Log error so that we can debug.
        }

        public void OnCompleted()
        {
            // This should never be invoked in practice. Log error so that we can debug.
        }
    }
}
