using System;
using System.Collections.Generic;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    internal class SimpleConsoleLogSink : ILogSink
    {
        public static readonly SimpleConsoleLogSink SingeltonInstance = new SimpleConsoleLogSink();

        public bool TryLogError(LoggingComponentName componentName, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            try
            {
                SimpleConsoleSink.Error(componentName.Part1, componentName.Part2, message, exception, dataNamesAndValues);
                return true;
            }
            catch
            {
                return false;
            }
            
        }

        public bool TryLogInfo(LoggingComponentName componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            try
            {
                SimpleConsoleSink.Info(componentName.Part1, componentName.Part2, message, dataNamesAndValues);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryLogDebug(LoggingComponentName componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            try
            {
                SimpleConsoleSink.Debug(componentName.Part1, componentName.Part2, message, dataNamesAndValues);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
