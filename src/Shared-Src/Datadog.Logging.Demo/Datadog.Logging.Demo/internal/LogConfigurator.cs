using System;
using System.Threading;
using Datadog.Logging.Composition;

namespace Datadog.Logging.Demo
{
    internal static class LogConfigurator
    {
        private const bool UseConsoleLogIfFileLogNotAvailable = true;
        private const int LogFileSizeBytes = 1024 * 50;

        private const string ProductFamily = "DotNet";
        private const string Product = "DemosAndTests";
        private const string ComponentGroup = "Logging-Demo";

        // If you copy this text, remember to re-generate a unique ID.
        private static readonly Guid LoggingDemoLogGroupId = Guid.Parse("8A335CC9-AAA7-435E-8794-87F9338ABFA2");

        private static ILogSink s_logSink = null;

        public static void SetupLogger()
        {
            try
            {
                if (DatadogEnvironmentFileLogSinkFactory.TryCreateNewFileLogSink(ProductFamily,
                                                                                 Product,
                                                                                 ComponentGroup,
                                                                                 LoggingDemoLogGroupId,
                                                                                 LogFileSizeBytes,
                                                                                 out FileLogSink fileLogSink))
                {
                    LogComposer.RedirectLogs(fileLogSink);
                    LogComposer.SetDebugLoggingEnabledBasedOnEnvironment();
                    s_logSink = fileLogSink;
                    return;
                }
            }
            catch
            { }

            if (UseConsoleLogIfFileLogNotAvailable)
            {
                s_logSink = SimpleConsoleLogSink.SingeltonInstance;
                LogComposer.RedirectLogs(s_logSink);
                LogComposer.IsDebugLoggingEnabled = true;
            }
        }

        public static void DisposeLogSink()
        {
            ILogSink logSink = Interlocked.Exchange(ref s_logSink, null);
            if (logSink != null && logSink is IDisposable disposableLogSink)
            {
                disposableLogSink.Dispose();
            }
        }
    }
}
