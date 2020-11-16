using System.Threading;
using Datadog.Trace;

namespace ManualInstrumentation
{
    public static class Program
    {
        public static void Main()
        {
            var tracer = Tracer.Instance;

            using (var rootScope = tracer.StartActive("root"))
            {
                Thread.Sleep(50);

                using (var child = tracer.StartActive("child"))
                {
                    child.Span.ResourceName = "child 1";
                    child.Span.Type = SpanTypes.Custom;
                    child.Span.SetTag("name1", "value1");
                    child.Span.SetTag("name2", "value2");
                    Thread.Sleep(100);
                }

                Thread.Sleep(50);

                using (var child = tracer.StartActive("child"))
                {
                    child.Span.ResourceName = "child 2";
                    child.Span.Type = SpanTypes.Custom;
                    child.Span.SetTag("name1", "José 😁🤞");
                    Thread.Sleep(100);
                }

                Thread.Sleep(50);
            }
        }
    }

    //public class CustomHttpClientApi : IApi
}
