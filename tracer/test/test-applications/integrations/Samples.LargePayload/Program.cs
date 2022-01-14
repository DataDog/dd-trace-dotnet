using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Datadog.Trace;

namespace Samples.LargePayload
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            ExitCode exitCode = 0;

            Parser.Default.ParseArguments<Options>(args)
               .WithParsed<Options>(o =>
               {
                   try
                   {
                       var traces = o.Traces;
                       var spansPerTrace = o.SpansPerTrace;

                       // Adjust to change the size of a trace
                       var traceTagFillerLength = o.SpanTagFillerLength;

                       Console.WriteLine($"Submitting {traces} traces with {spansPerTrace} spans each and filler tag length {traceTagFillerLength}.");

                       var traceFiller = string.Empty;
                       while (traceFiller.Length < traceTagFillerLength)
                       {
                           traceFiller += ".";
                       }


                       var traceTasks = new List<Task>();

                       while (traces-- > 0)
                       {
                           // Submit every trace close to at the same time
                           var traceTask = Task.Run(
                                          () =>
                                          {
                                              using (var traceScope = Tracer.Instance.StartActive("very-big-trace"))
                                              {
                                                  traceScope.Span.SetTag("fill", Guid.NewGuid().ToString());
                                                  traceScope.Span.SetTag("stuff", traceFiller);

                                                  var spansRemaining = spansPerTrace;

                                                  while (spansRemaining-- > 0)
                                                  {
                                                      using (var spanScope = Tracer.Instance.StartActive("nest"))
                                                      {
                                                          spanScope.Span.SetTag("fill", Guid.NewGuid().ToString());
                                                          spanScope.Span.SetTag("stuff", traceFiller);
                                                      }
                                                  }
                                              }
                                          });

                           traceTasks.Add(traceTask);
                       }

                       Console.WriteLine("Waiting for trace tasks to finish.");

                       Task.WaitAll(traceTasks.ToArray());

                       Console.WriteLine("Test complete, waiting for spans to flush.");

                       Tracer.Instance.ForceFlushAsync().Wait(); // Just make sure everything flushes

                       Console.WriteLine("Complete.");

                       exitCode = ExitCode.Success;
                   }
                   catch (Exception ex)
                   {
                       Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                       Console.Error.WriteLine(ex);
                       exitCode = ExitCode.UnknownError;
                   }
               });

            return (int)exitCode;
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
