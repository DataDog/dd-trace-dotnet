using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Logging.Composition;
using Datadog.Logging.Emission;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    internal static class LogConfigurator
    {
        private const bool UseConsoleLogInsteadOfFileLog = AssemblyLoader.UseConsoleLoggingInsteadOfFile;
        private const bool UseConsoleLogIfFileLogNotAvailable = AssemblyLoader.UseConsoleLoggingIfFileLoggingFails;

        private const string ProductFamily = "DotNet";
        private const string Product = "Common";
        private const string ComponentGroup = "ManagedLoader";

        /// <summary>A universe-wide, randomly generated unique ID for managed loader logs.</summary>
        private static readonly Guid ManagedLoaderLogGroupId = Guid.Parse("27B50088-5AFB-44AC-9F24-8A5BD5D1DCF6");

        private static readonly DefaultFormat.Options FileFormatOptions = new DefaultFormat.Options(useUtcTimestamps: false,
                                                                                                    useNewLinesInErrorMessages: true,
                                                                                                    useNewLinesInDataNamesAndValues: true);

        private static IReadOnlyDictionary<Type, LogSourceNameCompositionLogSink> s_logRedirections = null;

#pragma warning disable CS0162 // Unreachable code detected: Purposeful control using const bool fields in this class.
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
        }
#pragma warning restore CS0162 // Unreachable code detected

        public static void DisposeLogSinks()
        {
            IReadOnlyDictionary<Type, LogSourceNameCompositionLogSink> logRedirections = Interlocked.Exchange(ref s_logRedirections, null);
            if (logRedirections != null)
            {
                foreach (KeyValuePair<Type, LogSourceNameCompositionLogSink> redirection in logRedirections)
                {
                    if (redirection.Value != null)
                    {
                        try
                        {
                            redirection.Value.Dispose();
                        }
                        catch
                        { }
                    }
                }
            }
        }

        private static bool TrySetupFileLogger()
        {
            try
            {
                var filenameBaseInfo = new DatadogEnvironmentFileLogSinkFactory.FilenameBaseInfo(ProductFamily, Product, ComponentGroup);
                if (DatadogEnvironmentFileLogSinkFactory.TryCreateNewFileLogSink(filenameBaseInfo,
                                                                                 ManagedLoaderLogGroupId,
                                                                                 preferredLogFileDirectory: null,
                                                                                 FileFormatOptions,
                                                                                 out FileLogSink fileLogSink))
                {
                    RedirectLogs(fileLogSink, out s_logRedirections);
                    LogComposer.SetDebugLoggingEnabledBasedOnEnvironment();
                    return true;
                }
            }
            catch
            { }

            return false;
        }

        private static bool TrySetupConsoleLogger()
        {
            RedirectLogs(SimpleConsoleLogSink.SingeltonInstance, out s_logRedirections);
            LogComposer.IsDebugLoggingEnabled = true;
            return true;
        }

        private static void RedirectLogs(ILogSink logSink, out IReadOnlyDictionary<Type, LogSourceNameCompositionLogSink> redirections)
        {
            LogComposer.RedirectLogs(logSink, out redirections);
        }
    }
}
