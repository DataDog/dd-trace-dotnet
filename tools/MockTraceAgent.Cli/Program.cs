using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MockTraceAgent.Cli
{
    public class Program
    {
        public static void Main()
        {
            using (var agent = new TraceAgent())
            {
                agent.RequestDeserialized += TracesReceived;
                agent.Start(8126);

                Console.WriteLine($"Listening on http://localhost:{8126}");
                Console.WriteLine("Press CTRL+C to exit.");

                Thread.Sleep(-1);
            }
        }

        private static void TracesReceived(object sender, EventArgs<IList<IList<MockSpan>>> traces)
        {
            int traceCount = traces.Value.Count;
            int spanCount = traces.Value.SelectMany(t => t).Count();

            Console.WriteLine($"{traceCount} traces received with {spanCount} spans.");

            /*
            foreach (IList<Span> trace in traces.Value)
            {
                foreach (Span span in trace)
                {
                    Console.WriteLine($"TraceId={span.TraceId}, SpanId={span.SpanId}, Service={span.Service}, Name={span.Name}, Resource={span.Resource}");
                }
            }
            */
        }
    }
}
