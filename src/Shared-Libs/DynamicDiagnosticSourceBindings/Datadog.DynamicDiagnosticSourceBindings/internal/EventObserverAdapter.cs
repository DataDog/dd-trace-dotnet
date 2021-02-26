using System;
using System.Collections.Generic;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    //internal class EventObserverAdapter : IObserver<KeyValuePair<string, object>>
    //{
    //    private const string LogComonentMoniker = nameof(EventObserverAdapter);

    //    private readonly Action<string, object> _eventObserver;

    //    public EventObserverAdapter(Action<string, object> eventObserver)
    //    {
    //        _eventObserver = eventObserver;
    //    }

    //    public void OnNext(KeyValuePair<string, object> eventInfo)
    //    {
    //        Action<string, object> eventObserver = _eventObserver;
    //        if (eventObserver != null)
    //        {
    //            eventObserver(eventInfo.Key, eventInfo.Value);
    //        }
    //    }

    //    public void OnError(Exception error)
    //    {
    //        Log.Error(LogComonentMoniker, $"An exception was passed to the {nameof(OnError)}(..)-handler.", error);
    //    }

    //    public void OnCompleted()
    //    {
    //        Log.Error(LogComonentMoniker, $"The {nameof(OnCompleted)}(..)-handler was invoked. This was not expected and should be investogated");
    //    }
    //}
}
