using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.CosmosDb
{
    class CosmosEventListener : EventListener
    {
        public CosmosEventListener() 
        {
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            Console.WriteLine(eventSource.Name);
            if (eventSource.Name == "DocumentDBClient")
            {
                EnableEvents(eventSource, EventLevel.LogAlways, (EventKeywords)1);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            try
            {
                Console.WriteLine($"{eventData.EventId} - {eventData.EventName}");
                Console.WriteLine(eventData.Message, eventData.Payload.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                Console.WriteLine(eventData.Message);
                Console.WriteLine("eventData.Payload.Count: " + eventData.Payload.Count);
                Console.WriteLine(string.Join(", ", eventData.Payload));

            }

        }
    }
}
