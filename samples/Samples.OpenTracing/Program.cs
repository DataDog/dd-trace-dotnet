using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.OpenTracing;

namespace Samples.SqlServer
{
    internal static class Program
    {
        private static async Task Main()
        {
            var tracer = OpenTracingTracerFactory.WrapTracer(Tracer.Instance);

            // Manually create a span for sort orders
            using (var span = tracer.BuildSpan("manualtrace.sortorders").StartActive())
            {
                SortOrders();
            }

            await Task.Run(
                () =>
                {
                    // Manually create a span for sort orders running asynchronously
                    using (var span = tracer.BuildSpan("manualtrace.sortorders").StartActive())
                    {
                        SortOrders();
                    }
                });


            // Manually create a span for sort orders
            using (var span = tracer.BuildSpan("manualtrace.sortorders").StartActive())
            {
                SortOrders();
            }

using (var parentScope = Tracer.Instance.StartActive("manual.sortorders"))
{
    using (var childScope = Tracer.Instance.StartActive("manual.sortorders.child"))
    {
        // Nest using statements around code you would like to trace
        SortOrders();
        
    }
}


        }

        private static void SortOrders()
        {
            Thread.Sleep(50);
        }
    }
}
