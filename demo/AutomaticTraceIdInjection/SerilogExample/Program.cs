using System;
using Datadog.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;

namespace SerilogExample
{
    class Program
    {
        private static ILogger log;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("Pass the desired output format as the first argument: 'text', 'json', or 'json-compact'");
            }

            var loggerConfiguration = new LoggerConfiguration()
                                          .Enrich.FromLogContext()
                                          .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information);

            // Use the input argument to determine the formatter to use for the Console sink
            // Default configuration is explained here: https://github.com/serilog/serilog/wiki/Configuration-Basics
            var format = args[0].ToLower();
            if (format.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                loggerConfiguration = loggerConfiguration
                                      .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}Exception={Exception}{NewLine}Properties={Properties}{NewLine}");
            }
            else if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                loggerConfiguration = loggerConfiguration
                                      .WriteTo.Console(new JsonFormatter());
            }
            else if (format.Equals("json-compact", StringComparison.OrdinalIgnoreCase))
            {
                loggerConfiguration = loggerConfiguration
                                      .WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                throw new ArgumentException("Pass the desired output format as the first argument: 'text', 'json', or 'json-compact'");
            }

            log = loggerConfiguration.CreateLogger();
            using (var scope = Tracer.Instance.StartActive($"SerilogExample - Main() - {format}"))
            {
                using (LogContext.PushProperty("order-number", 1024))
                {
                    log.Information("Here's a message");
                }
            }
        }
    }
}
