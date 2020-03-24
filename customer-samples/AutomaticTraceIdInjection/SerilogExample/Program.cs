using System.IO;
using Datadog.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;

namespace SerilogExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // Regardless of the output layout, your LoggerConfiguration must be
            // enriched from the LogContext to extract the `dd.trace_id` and `dd.span_id`
            // properties that are automatically injected by the .NET tracer
            //
            // Additions to LoggerConfiguration:
            // - .Enrich.FromLogContext()
            var loggerConfiguration = new LoggerConfiguration()
                                          .Enrich.FromLogContext()
                                          .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information);

            // When using a message template, you must emit all properties using the {Properties} syntax in order to emit `dd.trace_id` and `dd.span_id` (see: https://github.com/serilog/serilog/wiki/Formatting-Output#formatting-plain-text)
            // This is because Serilog cannot look up these individual keys by name due to the '.' in the key name (see https://github.com/serilog/serilog/wiki/Writing-Log-Events#message-template-syntax)
            // Additionally, Datadog will only parse log properties if they are in a JSON-like map, and the values for dd.trace_id and dd.span_id must be surrounded by quotes
            //
            // Additions to layout:
            // - {Properties}
            //
            loggerConfiguration = loggerConfiguration
                                      .WriteTo.File(
                                          "log-Serilog-textFile.log",
                                          outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Properties} {Message:lj} {NewLine}{Exception}");

            // The built-in JsonFormatter will display all properties by default, so no extra work is needed to emit `dd.trace_id` and `dd.span_id`
            //
            // Additions to layout: none
            //
            loggerConfiguration = loggerConfiguration
                                      .WriteTo.File(
                                          new JsonFormatter(),
                                          "log-Serilog-jsonFile-allProperties.log");

            // The CompactJsonFormatter from the Serilog.Formatting.Compact NuGet package will display all properties by default, so no extra work is needed to emit `dd.trace_id` and `dd.span_id`
            //
            // Additions to layout: none
            //
            loggerConfiguration = loggerConfiguration
                                      .WriteTo.File(
                                          new CompactJsonFormatter(),
                                          "log-Serilog-compactJsonFile-allProperties.log");

            // Main procedure
            var log = loggerConfiguration.CreateLogger();
            using (LogContext.PushProperty("order-number", 1024))
            {
                log.Information("Message before a trace.");

                using (var scope = Tracer.Instance.StartActive("SerilogExample - Main()"))
                {
                    log.Information("Message during a trace.");
                }

                log.Information("Message after a trace.");
            }
        }
    }
}
