using System;
using System.Collections.Generic;

namespace Datadog.Logging.Emission
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    internal static class SimpleConsoleSink
    {
        private const bool UseNewLinesInErrorMessages = true;
        private const bool UseNewLinesInDataNamesAndValues = true;

        public const bool IsDebugLoggingEnabled = true;

        public static void Error(string componentName, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            Error(componentNamePart1: componentName, componentNamePart2: null, message, exception, dataNamesAndValues);
        }

        public static void Error(string componentNamePart1, string componentNamePart2, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            string errorMessage = DefaultFormat.ConstructErrorMessage(message, exception, UseNewLinesInErrorMessages);

            Console.WriteLine(Environment.NewLine
                            + DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Error,
                                                             componentNamePart1,
                                                             componentNamePart2,
                                                             useUtcTimestamp: false,
                                                             errorMessage,
                                                             dataNamesAndValues,
                                                             UseNewLinesInDataNamesAndValues)
                                           .ToString());
        }

        public static void Info(string componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            Info(componentNamePart1: componentName, componentNamePart2: null, message, dataNamesAndValues);
        }

        public static void Info(string componentNamePart1, string componentNamePart2, string message, IEnumerable<object> dataNamesAndValues)
        {
            Console.WriteLine(Environment.NewLine
                            + DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Info,
                                                             componentNamePart1,
                                                             componentNamePart2,
                                                             useUtcTimestamp: false,
                                                             message,
                                                             dataNamesAndValues,
                                                             UseNewLinesInDataNamesAndValues)
                                           .ToString());
        }

        public static void Debug(string componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            Debug(componentNamePart1: componentName, componentNamePart2: null, message, dataNamesAndValues);
        }

        public static void Debug(string componentNamePart1, string componentNamePart2, string message, IEnumerable<object> dataNamesAndValues)
        {
            Console.WriteLine(Environment.NewLine
                            + DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Debug,
                                                             componentNamePart1,
                                                             componentNamePart2,
                                                             useUtcTimestamp: false,
                                                             message,
                                                             dataNamesAndValues,
                                                             UseNewLinesInDataNamesAndValues)
                                           .ToString());
        }
    }
}
