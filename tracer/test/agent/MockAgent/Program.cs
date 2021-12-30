// See https://aka.ms/new-console-template for more information
using CommandLine;
using Datadog.Trace.TestHelpers;
using MockAgent;

var showTraces = true;
var showMetrics = true;

EventHandler<EventArgs<IList<IList<MockTracerAgent.Span>>>> displayTraces = (sender, args) =>
{
    if (!showTraces)
    {
        return;
    }

    var traces = args.Value;
    foreach (var trace in traces)
    {
        foreach (var span in trace)
        {
            Console.WriteLine(span);
        }
    }
};

EventHandler<EventArgs<string>> displayStats = (sender, args) =>
{
    if (!showMetrics)
    {
        return;
    }

    Console.WriteLine($"Stats: {args.Value}");
};

Parser.Default.ParseArguments<Options>(args)
       .WithParsed<Options>(o =>
       {
           var agents = new List<MockTracerAgent>();

           try
           {
               if (o.Tcp || args.Length == 0)
               {
                   var agent = new MockTracerAgent(port: o.TracesPort, useStatsd: true, requestedStatsDPort: o.MetricsPort);
                   Console.WriteLine($"Listening for traces on TCP: {agent.Port}");
                   Console.WriteLine($"Listening for metrics on UDP port: {agent.StatsdPort}");
                   agent.RequestDeserialized += displayTraces;
                   agent.MetricsReceived += displayStats;
                   agents.Add(agent);
               }

               if (o.UnixDomainSockets || args.Length == 0)
               {
                   var agent = new MockTracerAgent(new UnixDomainSocketConfig(o.TracesUnixDomainSocketPath, o.MetricsUnixDomainSocketPath));
                   Console.WriteLine($"Listening for traces on Unix Domain Socket: {agent.TracesUdsPath}");
                   Console.WriteLine($"Listening for metrics on Unix Domain Socket: {agent.StatsUdsPath}");
                   agent.RequestDeserialized += displayTraces;
                   agent.MetricsReceived += displayStats;
                   agents.Add(agent);
               }

               if (o.WindowsNamedPipe) // || args.Length == 0)
               {
                   var agent = new MockTracerAgent(new WindowsPipesConfig(o.TracesPipeName, o.MetricsPipeName));
                   Console.WriteLine($"Listening for traces on Windows Named Pipe: {agent.TracesWindowsPipeName}");
                   Console.WriteLine($"Listening for metrics on Windows Named Pipe: {agent.StatsWindowsPipeName}");
                   agent.RequestDeserialized += displayTraces;
                   agent.MetricsReceived += displayStats;
                   agents.Add(agent);
               }

               var shutdown = false;

               while (!shutdown)
               {
                   Console.WriteLine("Options - Q to exit, T to toggle show traces, M to toggle show metrics. ");
                   var input = Console.ReadKey();
                   var entry = input.KeyChar.ToString().ToLowerInvariant();
                   shutdown = entry.Contains("q");

                   if (entry.Contains("t"))
                   {
                       showTraces = !showTraces;
                       if (showTraces)
                       {
                           Console.WriteLine("Showing traces.");
                       }
                       else
                       {
                           Console.WriteLine("Hiding traces.");
                       }
                   }

                   if (entry.Contains("m"))
                   {
                       showMetrics = !showMetrics;
                       if (showMetrics)
                       {
                           Console.WriteLine("Showing metrics.");
                       }
                       else
                       {
                           Console.WriteLine("Hiding metrics.");
                       }
                   }
               }
           }
           finally
           {
               foreach (var agent in agents)
               {
                   agent?.Dispose();
               }
           }

       });
