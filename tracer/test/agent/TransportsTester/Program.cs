using System;
using System.Collections.Generic;
using System.Threading;
using CommandLine;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;

namespace TransportsTester
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            ExitCode exitCode = 0;
            var shutdown = false;

            EventHandler<EventArgs<string>> displayStats = (sender, args) =>
            {
                Console.WriteLine($"Stats: {args.Value}");
            };

            EventHandler<EventArgs<IList<IList<MockTracerAgent.Span>>>> traceCount = (sender, args) =>
            {
                Console.WriteLine($"trace count: {args.Value.Count}");
            };

            Parser.Default.ParseArguments<Options>(args)
               .WithParsed<Options>(o =>
               {

                   var agent = new MockTracerAgent(new UnixDomainSocketConfig(Environment.GetEnvironmentVariable("DD_APM_RECEIVER_SOCKET"), Environment.GetEnvironmentVariable("DD_DOGSTATSD_SOCKET")));
                   agent.MetricsReceived += displayStats;
                   agent.RequestDeserialized += traceCount;

                   try
                   {
                       if (o.QuietMode)
                       {
                           Console.WriteLine($"Quiet mode enabled. Configured to live for {o.SecondsToLive} seconds.");
                           var timeToStop = DateTime.Now.AddSeconds(o.SecondsToLive);

                           while (o.TraceCount-- > 0)
                           {
                               MakeTrace();
                           }

                           while (!shutdown)
                           {
                               shutdown = timeToStop <= DateTime.Now;
                               Thread.Sleep(50);
                           }
                       }
                       else
                       {
                           while (!shutdown)
                           {
                               Menu();
                               var entry = Console.ReadKey();
                               Console.WriteLine();
                               var keyChar = entry.KeyChar.ToString().ToUpperInvariant();
                               if (keyChar == "Q")
                               {
                                   return;
                               }
                               else if (keyChar == "H")
                               {
                                   Menu();
                               }
                               else
                               {
                                   MakeTrace();
                               }
                           }
                       }

                       exitCode = ExitCode.Success;
                   }
                   catch (Exception ex)
                   {
                       Console.WriteLine($"We have encountered an exception, the sample fails: {ex.Message}");
                       Console.Error.WriteLine(ex);
                       exitCode = ExitCode.UnknownError;
                   }
               });

            return (int)exitCode;
        }

        static void Menu()
        {
            Console.WriteLine("OPTIONS");
            Console.WriteLine("  Q - Exit");
            Console.WriteLine("  H - Help");
            Console.WriteLine("  Any Key - Send Trace");
        }

        static void MakeTrace()
        {
            using (var span = Tracer.Instance.StartActive("manual-span"))
            {
                Console.WriteLine("Span created");
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
