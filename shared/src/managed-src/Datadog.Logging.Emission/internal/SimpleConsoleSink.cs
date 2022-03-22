// <copyright file="SimpleConsoleSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Logging.Emission
{
    internal static class SimpleConsoleSink
    {
#pragma warning disable IDE1006  // Runtime-initialized Constants {
        private static readonly DefaultFormat.Options FormatOptions = DefaultFormat.Options.StructuredMultilines;
#pragma warning restore IDE1006  // } Runtime-initialized Constants

        public static void Error(
                            string logSourceNamePart1,
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

            Console.WriteLine(
                Environment.NewLine + DefaultFormat.LogLine.Construct(
                    DefaultFormat.LogLevelMoniker_Error,
                    logSourceNamePart1,
                    logSourceNamePart2,
                    logSourceCallLineNumber,
                    logSourceCallMemberName,
                    logSourceCallFileName,
                    logSourceAssemblyName,
                    errorMessage,
                    dataNamesAndValues,
                    FormatOptions.UseUtcTimestamps,
                    FormatOptions.UseNewLinesInDataNamesAndValues).ToString());
        }

        public static void Info(
                            string logSourceNamePart1,
                            string logSourceNamePart2,
                            int logSourceCallLineNumber,
                            string logSourceCallMemberName,
                            string logSourceCallFileName,
                            string logSourceAssemblyName,
                            string message,
                            IEnumerable<object> dataNamesAndValues)
        {
            Console.WriteLine(
                Environment.NewLine + DefaultFormat.LogLine.Construct(
                    DefaultFormat.LogLevelMoniker_Info,
                    logSourceNamePart1,
                    logSourceNamePart2,
                    logSourceCallLineNumber,
                    logSourceCallMemberName,
                    logSourceCallFileName,
                    logSourceAssemblyName,
                    message,
                    dataNamesAndValues,
                    FormatOptions.UseUtcTimestamps,
                    FormatOptions.UseNewLinesInDataNamesAndValues).ToString());
        }

        public static void Debug(
                            string logSourceNamePart1,
                            string logSourceNamePart2,
                            int logSourceCallLineNumber,
                            string logSourceCallMemberName,
                            string logSourceCallFileName,
                            string logSourceAssemblyName,
                            string message,
                            IEnumerable<object> dataNamesAndValues)
        {
            Console.WriteLine(Environment.NewLine + DefaultFormat.LogLine.Construct(
                DefaultFormat.LogLevelMoniker_Debug,
                logSourceNamePart1,
                logSourceNamePart2,
                logSourceCallLineNumber,
                logSourceCallMemberName,
                logSourceCallFileName,
                logSourceAssemblyName,
                message,
                dataNamesAndValues,
                FormatOptions.UseUtcTimestamps,
                FormatOptions.UseNewLinesInDataNamesAndValues).ToString());
        }
    }
}