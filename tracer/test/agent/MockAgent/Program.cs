// See https://aka.ms/new-console-template for more information
using CommandLine;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using MockAgent;

var configurationSource = new CompositeConfigurationSource
{
    new EnvironmentConfigurationSource(),
};

var exporterSettings = new ExporterSettings(configurationSource);

// Console.WriteLine($"Available Environment Variables");
// IDictionary currentEnvVars = Environment.GetEnvironmentVariables();
// if (currentEnvVars != null)
// {
//     foreach (DictionaryEntry item in currentEnvVars)
//     {
//         Console.WriteLine($"{item.Key}: {item.Value}");
//     }
// }


EventHandler<EventArgs<IList<IList<MockTracerAgent.Span>>>> displayTraces = (sender, args) =>
{
    var traces = args.Value;
    foreach (var trace in traces)
    {
        foreach (var span in trace)
        {
            Console.WriteLine(span);
        }
    }
};

Parser.Default.ParseArguments<Options>(args)
       .WithParsed<Options>(o =>
       {
           var agents = new List<MockTracerAgent>();

           try
           {
               if (o.Tcp || args.Length == 0)
               {
                   var agent = new MockTracerAgent(port: o.TracesPort, useStatsd: true);
                   Console.WriteLine($"Listening for traces on TCP: {o.TracesPort}");
                   Console.WriteLine($"Listening for metrics on UDP port: {o.MetricsPort}");
                   agent.RequestDeserialized += displayTraces;
                   agents.Add(agent);
               }

               if (o.UnixDomainSockets || args.Length == 0)
               {
                   var agent = new MockTracerAgent(traceUdsName: o.TracesUnixDomainSocketPath, statsUdsName: o.MetricsUnixDomainSocketPath);
                   Console.WriteLine($"Listening for traces on Unix Domain Socket: {o.TracesUnixDomainSocketPath}");
                   Console.WriteLine($"Listening for metrics on Unix Domain Socket: {o.MetricsUnixDomainSocketPath}");
                   agent.RequestDeserialized += displayTraces;
                   agents.Add(agent);
               }

               if (o.WindowsNamedPipe)
               {
                   // Console.WriteLine($"Listening for traces on Windows Named Pipe: {o.TracesPipeName}");
                   // Console.WriteLine($"Listening for metrics on Windows Named Pipe: {o.MetricsPipeName}");
                   throw new NotImplementedException("Mock Agent does not yet support windows named pipes.");
               }

               var shutdown = false;

               while (!shutdown)
               {
                   Console.WriteLine("Enter Q to exit: ");
                   var input = Console.ReadKey();
                   shutdown = input.KeyChar.ToString().ToLowerInvariant().Contains("q");
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
