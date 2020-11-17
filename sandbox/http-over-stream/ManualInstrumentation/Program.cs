using System;
using System.Threading;
using Datadog.Trace;

namespace ManualInstrumentation
{
    public static class Program
    {
        public static void Main()
        {
            var prefix = "pipe";
            var tracer = Tracer.Instance;

            var iterations = 5;

            while (iterations-- > 0)
            {

                using (var rootScope = tracer.StartActive($"{prefix}.root"))
                {
                    Thread.Sleep(50);

                    using (var child = tracer.StartActive($"{prefix}.child"))
                    {
                        child.Span.ResourceName = "child 1";
                        child.Span.Type = SpanTypes.Custom;
                        child.Span.SetTag("name1", "value1");
                        child.Span.SetTag("name2", "value2");
                        Thread.Sleep(100);
                    }

                    Thread.Sleep(50);

                    using (var child = tracer.StartActive($"{prefix}.child"))
                    {
                        child.Span.ResourceName = "child 2";
                        child.Span.Type = SpanTypes.Custom;
                        child.Span.SetTag("name1", "José 😁🤞");
                        Thread.Sleep(100);
                    }

                    Thread.Sleep(50);
                }

                Thread.Sleep(2000);

            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    //public class CustomHttpClientApi : IApi
}
