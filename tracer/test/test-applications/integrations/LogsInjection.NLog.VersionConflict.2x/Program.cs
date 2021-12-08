using System;
using System.IO;
using Datadog.Trace;
using NLog;
using NLog.Config;
using Samples;

namespace LogsInjection.NLog.VersionConflict_2x
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
            var appDirectory = Directory.GetParent(typeof(Program).Assembly.Location).FullName;
            var textFilePath = Path.Combine(appDirectory, "log-textFile.log");
            var jsonFilePath = Path.Combine(appDirectory, "log-jsonFile.log");

            File.Delete(textFilePath);
            File.Delete(jsonFilePath);

            // Initialize NLog
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(appDirectory, "NLog.46.config"));
            Logger logger = LogManager.GetCurrentClassLogger();

            try
            {
                RunLoggingProcedure(logger);
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

        private static void RunLoggingProcedure(Logger logger)
        {
            logger.Info($"{ExcludeMessagePrefix}Starting manual1 Datadog scope.");
            using (Tracer.Instance.StartActive("manual1"))
            {
                logger.Info($"Trace: manual1");
                using (TracerUtils.StartAutomaticTrace("automatic2"))
                {
                    logger.Info($"Trace: manual1-automatic2");
                    using (Tracer.Instance.StartActive("manual3"))
                    {
                        logger.Info($"Trace: manual1-automatic2-manual3");
                        using (Tracer.Instance.StartActive("manual4"))
                        {
                            logger.Info($"Trace: manual1-automatic2-manual3-manual4");

                            using (TracerUtils.StartAutomaticTrace("automatic5"))
                            {
                                logger.Info($"Trace: manual1-automatic2-manual3-manual4-automatic5");
                            }

                            logger.Info($"Trace: manual1-automatic2-manual3-manual4");
                        }

                        logger.Info($"Trace: manual1-automatic2-manual3");

                        using (TracerUtils.StartAutomaticTrace("automatic4"))
                        {
                            logger.Info($"Trace: manual1-automatic2-manual3-automatic4");
                            using (TracerUtils.StartAutomaticTrace("automatic5"))
                            {
                                logger.Info($"Trace: manual1-automatic2-manual3-automatic4-automatic5");
                                using (Tracer.Instance.StartActive("manual6"))
                                {
                                    logger.Info($"Trace: manual1-automatic2-manual3-automatic4-automatic5-manual6");
                                }

                                logger.Info($"Trace: manual1-automatic2-manual3-automatic4-automatic5");
                            }

                            logger.Info($"Trace: manual1-automatic2-manual3-automatic4");
                        }

                        logger.Info($"Trace: manual1-automatic2-manual3");
                    }
                    logger.Info($"Trace: manual1-automatic2");
                }

                logger.Info($"Trace: manual1");
            }

            logger.Info($"{ExcludeMessagePrefix}Exited manual1 Datadog scope.");
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
