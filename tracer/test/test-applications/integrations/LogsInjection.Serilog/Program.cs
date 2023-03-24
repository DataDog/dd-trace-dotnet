
using System;
using System.IO;
#if SERILOG_2_12
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
#endif
using PluginApplication;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace LogsInjection.Serilog
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // This test creates and unloads an appdomain
            // It seems that in some (unknown) conditions the tracer gets loader into the child appdomain
            // When that happens, there is a risk that the startup log thread gets aborted during appdomain unload,
            // adding error logs which in turn cause a failure in CI.
            // Disabling the startup log at the process level should prevent this.
            Environment.SetEnvironmentVariable("DD_TRACE_STARTUP_LOGS", "0");

            LoggingMethods.DeleteExistingLogs();

            // Initialize Serilog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");
            var jsonFilePath = Path.Combine(appDirectory, "log-jsonFile.log");
            var useConfiguration = Environment.GetEnvironmentVariable("SERILOG_CONFIGURE_FROM_APPSETTINGS") == "1";

            LoggerConfiguration configuration;
            if (useConfiguration)
            {
#if SERILOG_2_12
                System.Console.WriteLine("Reading configuration...");
                var appSettingsConfig = new ConfigurationBuilder()
                                       .SetBasePath(appDirectory)
                                       .AddJsonFile("appsettings.json")
                                       .AddInMemoryCollection(new Dictionary<string, string>
                                        {
                                            { "Serilog:WriteTo:0:Args:configureLogger:WriteTo:0:Args:path", textFilePath },
                                            { "Serilog:WriteTo:1:Args:path", jsonFilePath },
                                        })
                                       .Build();

                configuration = new LoggerConfiguration()
                               .ReadFrom.Configuration(appSettingsConfig);
                System.Console.WriteLine("Building logger...");
#else
                throw new Exception("Unable to load from configuration in Serilog <2.12.0");
#endif
            }
            else
            {
                configuration = new LoggerConfiguration()
                               .Enrich.FromLogContext()
                               .MinimumLevel.Is(LogEventLevel.Information)
                               .WriteTo.Logger(
                                    lc => lc
#if SERILOG_2_12
                                         .Filter.ByExcluding("RequestPath like '/health%'")
#endif
                                         .WriteTo.File(
                                                 textFilePath,
                                                 outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {{ dd_service: \"{dd_service}\", dd_version: \"{dd_version}\", dd_env: \"{dd_env}\", dd_trace_id: \"{dd_trace_id}\", dd_span_id: \"{dd_span_id}\" }} {Message:lj} {NewLine}{Exception}")
                                        .WriteTo.Logger(lc2 => lc2.WriteTo.Console()))
#if SERILOG_2_0
#if SERILOG_2_12
                               .Filter.ByExcluding("RequestPath like '/health%'")
#endif
                               .WriteTo.File(
                                    new JsonFormatter(),
                                    jsonFilePath)
#endif
                               .WriteTo.Logger(lc => lc.WriteTo.Console());
            }

            var log = configuration.CreateLogger();

#if SERILOG_2_12
            log.Information("[ExcludeMessage] This method is excluded from log files because {RequestPath} matches '/health%', but is still sent via direct log submission", "/healthz");
#endif

            return LoggingMethods.RunLoggingProcedure(LogWrapper(log));
        }

#if SERILOG_2_0
        public static Action<string> LogWrapper(Logger log)
        {
            return (string message) => log.Information(message);
        }

        public static LoggerConfiguration Console(this LoggerSinkConfiguration sinkConfiguration)
        {
            return sinkConfiguration.Sink( new ConsoleSink());
        }
#else
        public static Action<string> LogWrapper(ILogger log)
        {
            return (string message) => log.Information(message);
        }
#endif
    }

#if SERILOG_2_0
    public class ConsoleSink : ILogEventSink
    {
        // dummy ConsoleSink to avoid deps 
        public void Emit(LogEvent logEvent)
        {
            Console.WriteLine($"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss} [{logEvent.Level}] {logEvent.RenderMessage()}{Environment.NewLine}{logEvent.Exception}");
        }
    }
#endif
}
