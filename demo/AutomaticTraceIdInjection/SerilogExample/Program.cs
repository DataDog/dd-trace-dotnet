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
            // Delete all log files from the last run
            File.Delete("log-Serilog-textFile-allProperties.log");
            File.Delete("log-Serilog-jsonFile-allProperties.log");
            File.Delete("log-Serilog-compactJsonFile-allProperties.log");

            var loggerConfiguration = new LoggerConfiguration()
                                          .Enrich.FromLogContext()
                                          .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information);

            // When using a message template, you must emit all properties in order to emit `dd.trace_id` and `dd.span_id`
            // This is because Serilog cannot look up these individual keys by name due to the '.' in the key name (see https://github.com/serilog/serilog/wiki/Writing-Log-Events#message-template-syntax)
            loggerConfiguration = loggerConfiguration
                                      .WriteTo.File(
                                          "log-Serilog-textFile-allProperties.log",
                                          // Use {Properties} to print out all property values
                                          outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} | Exception={Exception} | Properties={Properties}{NewLine}");

            // The built-in JsonFormatter will display all properties by default,
            // so no extra work is needed to emit `dd.trace_id` and `dd.span_id`
            loggerConfiguration = loggerConfiguration
                                      .WriteTo.File(
                                          new JsonFormatter(),
                                          "log-Serilog-jsonFile-allProperties.log");

            // The CompactJsonFormatter from the Serilog.Formatting.Compact NuGet package will display all properties by default,
            // so no extra work is needed to emit `dd.trace_id` and `dd.span_id`
            loggerConfiguration = loggerConfiguration
                                      .WriteTo.File(
                                          new CompactJsonFormatter(),
                                          "log-Serilog-compactJsonFile-allProperties.log");

            // Main procedure
            var log = loggerConfiguration.CreateLogger();
            using (LogContext.PushProperty("order-number", 1024))
            {
                using (var scope = Tracer.Instance.StartActive("SerilogExample - Main()"))
                {
                    log.Information("Message inside a trace.");
                }

                log.Information("Message outside a trace.");
            }
        }
    }
}
