
using System;
using System.IO;
using Datadog.Trace;
using Samples;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Json;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace LogsInjection.Serilog.VersionConflict_2x
{
    public class Program
    {
        /// <summary>
        /// Prepend a string to log lines that should not be validated for logs injection.
        /// In other words, they're not written within a Datadog scope
        /// </summary>
        private static readonly string ExcludeMessagePrefix = "[ExcludeMessage]";

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

            try
            {
                RunLoggingProcedure(log);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

#if NETCOREAPP2_1
            // Add a delay to avoid a race condition on shutdown: https://github.com/dotnet/coreclr/pull/22712
            // This would cause a segmentation fault on .net core 2.x
            System.Threading.Thread.Sleep(5000);
#endif

            return (int)ExitCode.Success;
        }

        private static void RunLoggingProcedure(Logger log)
        {
            log.Information($"{ExcludeMessagePrefix}Starting manual1 Datadog scope.");
            using (Tracer.Instance.StartActive("manual1"))
            {
                log.Information($"Trace: manual1");
                using (TracerUtils.StartAutomaticTrace("automatic2"))
                {
                    log.Information($"Trace: manual1-automatic2");
                    using (Tracer.Instance.StartActive("manual3"))
                    {
                        log.Information($"Trace: manual1-automatic2-manual3");
                        using (Tracer.Instance.StartActive("manual4"))
                        {
                            log.Information($"Trace: manual1-automatic2-manual3-manual4");

                            using (TracerUtils.StartAutomaticTrace("automatic5"))
                            {
                                log.Information($"Trace: manual1-automatic2-manual3-manual4-automatic5");
                            }

                            log.Information($"Trace: manual1-automatic2-manual3-manual4");
                        }

                        log.Information($"Trace: manual1-automatic2-manual3");

                        using (TracerUtils.StartAutomaticTrace("automatic4"))
                        {
                            log.Information($"Trace: manual1-automatic2-manual3-automatic4");
                            using (TracerUtils.StartAutomaticTrace("automatic5"))
                            {
                                log.Information($"Trace: manual1-automatic2-manual3-automatic4-automatic5");
                                using (Tracer.Instance.StartActive("manual6"))
                                {
                                    log.Information($"Trace: manual1-automatic2-manual3-automatic4-automatic5-manual6");
                                }

                                log.Information($"Trace: manual1-automatic2-manual3-automatic4-automatic5");
                            }

                            log.Information($"Trace: manual1-automatic2-manual3-automatic4");
                        }

                        log.Information($"Trace: manual1-automatic2-manual3");
                    }
                    log.Information($"Trace: manual1-automatic2");
                }

                log.Information($"Trace: manual1");
            }

            log.Information($"{ExcludeMessagePrefix}Exited manual1 Datadog scope.");
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
