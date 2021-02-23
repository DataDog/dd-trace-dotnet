using System;

namespace Datadog.Logging.Emission
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    internal static class SimpleConsoleSink
    {
        public const bool IsDebugLoggingEnabled = true;

        public static void Error(string componentName, string message, Exception exception, params object[] dataNamesAndValues)
        {
            Error(componentNamePart1: componentName, componentNamePart2: null, message, exception, dataNamesAndValues);
        }

        public static void Error(string componentNamePart1, string componentNamePart2, string message, Exception exception, params object[] dataNamesAndValues)
        {
            string errorMessage = DefaultFormat.ConstructErrorMessage(message, exception, useNewLines: true);

            Console.WriteLine();
            Console.WriteLine(DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Error,
                                                             componentNamePart1,
                                                             componentNamePart2,
                                                             useUtcTimestamp: false,
                                                             errorMessage,
                                                             dataNamesAndValues)
                                           .ToString());
        }

        public static void Info(string componentName, string message, params object[] dataNamesAndValues)
        {
            Info(componentNamePart1: componentName, componentNamePart2: null, message, dataNamesAndValues);
        }

        public static void Info(string componentNamePart1, string componentNamePart2, string message, params object[] dataNamesAndValues)
        {
            Console.WriteLine();
            Console.WriteLine(DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Info,
                                                             componentNamePart1,
                                                             componentNamePart2,
                                                             useUtcTimestamp: false,
                                                             message,
                                                             dataNamesAndValues)
                                           .ToString());
        }

        public static void Debug(string componentName, string message, params object[] dataNamesAndValues)
        {
            Info(componentNamePart1: componentName, componentNamePart2: null, message, dataNamesAndValues);
        }

        public static void Debug(string componentNamePart1, string componentNamePart2, string message, params object[] dataNamesAndValues)
        {
            Console.WriteLine();
            Console.WriteLine(DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Info,
                                                             componentNamePart1,
                                                             componentNamePart2,
                                                             useUtcTimestamp: false,
                                                             message,
                                                             dataNamesAndValues)
                                           .ToString());
        }
    }
}
