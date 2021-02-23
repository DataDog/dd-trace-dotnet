using System;
using System.Threading;
using Datadog.Logging.Composition;

namespace Datadog.Logging.Demo
{
    internal static class LogConfigurator
    {
        private const bool UseConsoleLogInsteadOfFileLog = false;
        private const bool UseConsoleLogIfFileLogNotAvailable = true;
        private const int LogFileSizeBytes = 1024 * 50;

        private const string ProductFamily = "DotNet";
        private const string Product = "DemosAndTests";
        private const string ComponentGroup = "Logging-Demo";

        // If you copy this text, remember to re-generate a unique ID.
        private static readonly Guid LoggingDemoLogGroupId = Guid.Parse("8A335CC9-AAA7-435E-8794-87F9338ABFA2");

        private static ILogSink s_logSink = null;

#pragma warning disable CS0162 // Unreachable code detected: Purposeful control using const bools.
        public static void SetupLogger()
        {
            if (UseConsoleLogInsteadOfFileLog)
            {
                if (TrySetupConsoleLogger())
                {
                    return;
                }
            }
            else
            {
                if (TrySetupFileLogger())
                {
                    return;
                }
            }

            if (UseConsoleLogIfFileLogNotAvailable)
            {
                if (TrySetupConsoleLogger())
                {
                    return;
                }
            }

            Console.ReadLine();
            Console.WriteLine($"Console-Message: PROBLEM! Could not setup logger.");
        }
#pragma warning restore CS0162 // Unreachable code detected

        public static void DisposeLogSink()
        {
            ILogSink logSink = Interlocked.Exchange(ref s_logSink, null);
            if (logSink != null && logSink is IDisposable disposableLogSink)
            {
                disposableLogSink.Dispose();
            }
        }

        private static bool TrySetupFileLogger()
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
                    return true;
                }
            }
            catch
            { }

            return false;
        }

        private static bool TrySetupConsoleLogger()
        {
            s_logSink = SimpleConsoleLogSink.SingeltonInstance;
            LogComposer.RedirectLogs(s_logSink);
            LogComposer.IsDebugLoggingEnabled = true;
            return true;
        }
    }
}
