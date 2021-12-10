
using System.IO;
using LogsInjectionHelper.VersionConflict;
using Serilog;
using Serilog.Formatting.Json;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace LogsInjection.Serilog.VersionConflict_2x
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // Initialize Serilog
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");
            var jsonFilePath = Path.Combine(appDirectory, "log-jsonFile.log");

            File.Delete(textFilePath);
            File.Delete(jsonFilePath);

            var log = new LoggerConfiguration()
                                        .Enrich.FromLogContext()
                                        .MinimumLevel.Is(LogEventLevel.Information)
                                        .WriteTo.File(
                                            textFilePath,
                                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {{ dd_service: \"{dd_service}\", dd_version: \"{dd_version}\", dd_env: \"{dd_env}\", dd_trace_id: \"{dd_trace_id}\", dd_span_id: \"{dd_span_id}\" }} {Message:lj} {NewLine}{Exception}")
                                        .WriteTo.File(
                                            new JsonFormatter(),
                                            jsonFilePath)
                                        .CreateLogger();

            return LoggingMethods.RunLoggingProcedure(log.Information);
        }
    }
}
