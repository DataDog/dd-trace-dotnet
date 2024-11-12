using System.Diagnostics.Tracing;

namespace DataDogThreadTest
{
    public class MyEventListener : EventListener
    {
        public MyEventListener()
        {
            EventSourceCreated += (_, e) => EnableEventSource(e.EventSource);
        }

        private void EnableEventSource(EventSource eventSource)
        {
            EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
        }
    }

    class Program
    {
        static EventListener _listener;

        static void Main(string[] args)
        {
            _listener = new MyEventListener();
        }
    }
}
