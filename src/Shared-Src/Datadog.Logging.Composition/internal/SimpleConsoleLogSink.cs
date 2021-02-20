using System;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    internal class SimpleConsoleLogSink : ILogSink
    {
        public static readonly SimpleConsoleLogSink SingeltonInstance = new SimpleConsoleLogSink();

        public void Error(StringPair componentName, string message, Exception exception, params object[] dataNamesAndValues)
        {
            SimpleConsoleSink.Error(componentName.Item1, componentName.Item2, message, exception, dataNamesAndValues);
        }

        public void Info(StringPair componentName, string message, params object[] dataNamesAndValues)
        {
            SimpleConsoleSink.Info(componentName.Item1, componentName.Item2, message, dataNamesAndValues);
        }

        public void Debug(StringPair componentName, string message, params object[] dataNamesAndValues)
        {
            SimpleConsoleSink.Debug(componentName.Item1, componentName.Item2, message, dataNamesAndValues);
        }
    }
}
