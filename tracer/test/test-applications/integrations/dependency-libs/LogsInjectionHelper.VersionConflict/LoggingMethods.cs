using System;
using Datadog.Trace;

namespace LogsInjectionHelper.VersionConflict
{
    public static class LoggingMethods
    {
        /// <summary>
        /// Prepend a string to log lines that should not be validated for logs injection.
        /// In other words, they're not written within a Datadog scope 
        /// </summary>
        private static readonly string ExcludeMessagePrefix = "[ExcludeMessage]";

        public static int RunLoggingProcedure(Action<string> logAction)
        {
            try
            {
                logAction($"{ExcludeMessagePrefix}Starting manual1 Datadog scope.");
                using (Tracer.Instance.StartActive("manual1"))
                {
                    logAction($"Trace: manual1");
                    using (TracerUtils.StartAutomaticTraceHigherAssemblyVersion("automatic2"))
                    {
                        logAction($"Trace: manual1-automatic2");
                        using (Tracer.Instance.StartActive("manual3"))
                        {
                            logAction($"Trace: manual1-automatic2-manual3");
                            using (Tracer.Instance.StartActive("manual4"))
                            {
                                logAction($"Trace: manual1-automatic2-manual3-manual4");

                                using (TracerUtils.StartAutomaticTraceHigherAssemblyVersion("automatic5"))
                                {
                                    logAction($"Trace: manual1-automatic2-manual3-manual4-automatic5");
                                }

                                logAction($"Trace: manual1-automatic2-manual3-manual4");
                            }

                            logAction($"Trace: manual1-automatic2-manual3");

                            using (TracerUtils.StartAutomaticTraceHigherAssemblyVersion("automatic4"))
                            {
                                logAction($"Trace: manual1-automatic2-manual3-automatic4");
                                using (TracerUtils.StartAutomaticTraceHigherAssemblyVersion("automatic5"))
                                {
                                    logAction($"Trace: manual1-automatic2-manual3-automatic4-automatic5");
                                    using (Tracer.Instance.StartActive("manual6"))
                                    {
                                        logAction($"Trace: manual1-automatic2-manual3-automatic4-automatic5-manual6");
                                    }

                                    logAction($"Trace: manual1-automatic2-manual3-automatic4-automatic5");
                                }

                                logAction($"Trace: manual1-automatic2-manual3-automatic4");
                            }

                            logAction($"Trace: manual1-automatic2-manual3");
                        }
                        logAction($"Trace: manual1-automatic2");
                    }

                    logAction($"Trace: manual1");
                }

                logAction($"{ExcludeMessagePrefix}Exited manual1 Datadog scope.");
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

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
