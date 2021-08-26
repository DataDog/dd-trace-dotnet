using System;
using System.Collections.Generic;

namespace Datadog.Logging.Emission
{
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    internal static class SimpleConsoleSink
    {
#pragma warning disable IDE1006  // Runtime-initialized Constants {
        private static readonly DefaultFormat.Options FormatOptions = DefaultFormat.Options.StructuredMultilines;
#pragma warning restore IDE1006  // } Runtime-initialized Constants

        public static void Error(string logSourceNamePart1,
                                 string logSourceNamePart2,
                                 int logSourceCallLineNumber,
                                 string logSourceCallMemberName,
                                 string logSourceCallFileName,
                                 string logSourceAssemblyName,
                                 string message,
                                 Exception exception,
                                 IEnumerable<object> dataNamesAndValues)
        {
            string errorMessage = DefaultFormat.ErrorMessage.Construct(message, exception, FormatOptions.UseNewLinesInErrorMessages);

            Console.WriteLine(Environment.NewLine
                            + DefaultFormat.LogLine.Construct(DefaultFormat.LogLevelMoniker_Error,
                                                              logSourceNamePart1,
                                                              logSourceNamePart2,
                                                              logSourceCallLineNumber,
                                                              logSourceCallMemberName,
                                                              logSourceCallFileName,
                                                              logSourceAssemblyName,
                                                              errorMessage,
                                                              dataNamesAndValues,
                                                              FormatOptions.UseUtcTimestamps,
                                                              FormatOptions.UseNewLinesInDataNamesAndValues)
                                                   .ToString());
        }

        public static void Info(string logSourceNamePart1,
                                string logSourceNamePart2,
                                int logSourceCallLineNumber,
                                string logSourceCallMemberName,
                                string logSourceCallFileName,
                                string logSourceAssemblyName,
                                string message,
                                IEnumerable<object> dataNamesAndValues)
        {
            Console.WriteLine(Environment.NewLine
                            + DefaultFormat.LogLine.Construct(DefaultFormat.LogLevelMoniker_Info,
                                                              logSourceNamePart1,
                                                              logSourceNamePart2,
                                                              logSourceCallLineNumber,
                                                              logSourceCallMemberName,
                                                              logSourceCallFileName,
                                                              logSourceAssemblyName,
                                                              message,
                                                              dataNamesAndValues,
                                                              FormatOptions.UseUtcTimestamps,
                                                              FormatOptions.UseNewLinesInDataNamesAndValues)
                                                   .ToString());
        }

        public static void Debug(string logSourceNamePart1,
                                 string logSourceNamePart2,
                                 int logSourceCallLineNumber,
                                 string logSourceCallMemberName,
                                 string logSourceCallFileName,
                                 string logSourceAssemblyName,
                                 string message,
                                 IEnumerable<object> dataNamesAndValues)
        {
            Console.WriteLine(Environment.NewLine
                            + DefaultFormat.LogLine.Construct(DefaultFormat.LogLevelMoniker_Debug,
                                                              logSourceNamePart1,
                                                              logSourceNamePart2,
                                                              logSourceCallLineNumber,
                                                              logSourceCallMemberName,
                                                              logSourceCallFileName,
                                                              logSourceAssemblyName,
                                                              message,
                                                              dataNamesAndValues,
                                                              FormatOptions.UseUtcTimestamps,
                                                              FormatOptions.UseNewLinesInDataNamesAndValues)
                                                   .ToString());
        }
    }
}
