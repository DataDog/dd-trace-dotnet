#if !NETCOREAPP2_1

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
                logAction($"{ExcludeMessagePrefix}Starting scopes.");

                // Start a trace using the latest automatic tracer so that we
                // automatically create a 128-bit trace-id and properly set the _dd.p.tid tag.
                // This is only done to make the testing logic easier, since all of the trace-id's
                // will have 128-bit trace-id's.
                //
                // Note: This is not necessary for real applications, as the backend will still be able to
                // correlate the 64-bit trace-id's from the manual side and the 128-bit trace-id's
                // from the automatic side.
                using (TracerUtils.StartAutomaticTraceHigherAssemblyVersion("automatic0"))
                {
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
                }

                logAction($"{ExcludeMessagePrefix}Exited scopes.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
#endif
