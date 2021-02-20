using System;
using Datadog.Logging.Composition;

namespace Datadog.AutoInstrumentation.ManagedLoader
{
    internal static class LogConfigurator
    {
        private const bool UseConsoleLogIfFileLogNotAvailable = true;

        private const string ProductFamily = "DotNet";
        private const string Product = "Common";
        private const string ComponentGroup = "ManagedLoader";
        private static readonly Guid ManagedLoaderLogGroupId = Guid.Parse("27B50088-5AFB-44AC-9F24-8A5BD5D1DCF6");

        public static void SetupLogger()
        {
            try
            {
                if (DatadogEnvironmentFileLogSinkFactory.TryCreateNewFileLogSink(ProductFamily, Product, ComponentGroup, ManagedLoaderLogGroupId, out FileLogSink fileLogSink))
                {
                    LogComposer.RedirectLogs(fileLogSink);
                    LogComposer.SetDebugLoggingEnabledBasedOnEnvironment();
                    return;
                }
            }
            catch
            { }

            if (UseConsoleLogIfFileLogNotAvailable)
            {
                LogComposer.RedirectLogs(SimpleConsoleLogSink.SingeltonInstance);
                LogComposer.IsDebugLoggingEnabled = true;
            }
        }
    }
}
