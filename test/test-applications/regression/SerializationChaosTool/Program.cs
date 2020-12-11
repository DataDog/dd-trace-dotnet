using System;
using System.Linq;
using System.Threading;
using Datadog.Trace;

namespace SerializationChaosTool
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                // Each trace is approximately 500kb
                // Meaning 20 traces is approximately our 10MB cap
                var traces = 20;
                var spansPerTrace = 1983;

                if (args.Any())
                {
                    if (int.TryParse(args[0], out var traceCountOverride))
                    {
                        traces = traceCountOverride;
                    }
                }

                var transport = Environment.GetEnvironmentVariable("DD_TRACE_TRANSPORT");

                while (traces-- > 0)
                {
                    using (var traceScope = Tracer.Instance.StartActive("very-big-trace"))
                    {
                        traceScope.Span.SetTag("test", transport);
                        traceScope.Span.SetTag("id", Guid.NewGuid().ToString());

                        var spansRemaining = spansPerTrace;

                        while (spansRemaining-- > 0)
                        {
                            using (var spanScope = Tracer.Instance.StartActive("nest"))
                            {
                                spanScope.Span.SetTag("fill", Guid.NewGuid().ToString());
                                spanScope.Span.SetTag("abcd", Guid.NewGuid().ToString());
                            }
                        }
                    }
                }

                Thread.Sleep(3000); // Just make sure everything flushes

                return (int)ExitCode.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
