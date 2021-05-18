using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace LargePayload
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                // Each trace is approximately 500kb
                // Meaning 20 traces is approximately 10MB and 100 traces is approximately 50MB
                var traces = 100;
                var spansPerTrace = 2000;
                var totalOverload = false;

                // Adjust to change the size of a trace
                const int justAtBound = 13980;
                const int justAboveBound = 13982;
                var traceTagFillerLength = justAtBound;

                // args = new[] { "insane" };
                // args = new[] { "overbound" };

                if (args.Any())
                {
                    if (int.TryParse(args[0], out var traceCountOverride))
                    {
                        traces = traceCountOverride;
                    }
                    else if (args[0].Equals("insane"))
                    {
                        totalOverload = true;
                    }
                    else if (args[0].Equals("overbound"))
                    {
                        traceTagFillerLength = justAboveBound;
                    }
                }

                Console.WriteLine($"Submitting {traces} traces with {spansPerTrace} spans each.");

                if (totalOverload)
                {
                    Console.WriteLine("Adding ridiculous amount of tags to overload the agent and fail the submit.");
                }

                var transport = Environment.GetEnvironmentVariable("DD_TRACE_TRANSPORT");

                var staticLength = 30;
                transport = transport ?? string.Empty;

                while (transport.Length < staticLength)
                {
                    transport += "_";
                }

                var traceFiller = "_trace_filler_";
                while (traceFiller.Length < traceTagFillerLength)
                {
                    traceFiller += "_";
                }


                var traceTasks = new List<Task>();

                while (traces-- > 0)
                {
                    // Submit every trace together
                    var traceTask = Task.Run(
                                   () =>
                                   {
                                       using (var traceScope = Tracer.Instance.StartActive("very-big-trace"))
                                       {
                                           traceScope.Span.SetTag("transport", transport);
                                           traceScope.Span.SetTag("fill", Guid.NewGuid().ToString());
                                           traceScope.Span.SetTag("stuff", traceFiller);

                                           var spansRemaining = spansPerTrace;

                                           while (spansRemaining-- > 0)
                                           {
                                               using (var spanScope = Tracer.Instance.StartActive("nest"))
                                               {
                                                   spanScope.Span.SetTag("fill", Guid.NewGuid().ToString());
                                                   spanScope.Span.SetTag("abcdefgh", Guid.NewGuid().ToString());
                                                   if (totalOverload)
                                                   {
                                                       var overloadTags = 10;
                                                       while (overloadTags-- > 0)
                                                       {
                                                           spanScope.Span.SetTag(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                                                       }
                                                   }
                                               }
                                           }
                                       }
                                   });

                    traceTasks.Add(traceTask);
                }

                Console.WriteLine("Waiting for trace tasks to finish.");

                Task.WaitAll(traceTasks.ToArray());

                Console.WriteLine("Test complete, waiting for spans to flush.");

                Thread.Sleep(1500); // Just make sure everything flushes

                Console.WriteLine("Complete.");

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
