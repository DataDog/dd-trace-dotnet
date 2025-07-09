// <copyright file="SingleLineTextFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;

namespace Datadog.Trace.Logging.Internal.TextFormatters;

internal class SingleLineTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        // write the timestamp, source, and log level to the output
        var utcTimestamp = logEvent.Timestamp.ToUniversalTime();
        var logLevel = LevelOutputFormat.GetLevelMoniker(logEvent.Level, "u3");
        output.Write($"[{utcTimestamp:yyyy-MM-dd HH:mm:ss.fff zzz} | DD_TRACE_DOTNET {TracerConstants.ThreePartVersion} | {logLevel}] ");

        // write the message to the output, using the template and properties
        logEvent.RenderMessage(output, output.FormatProvider);

        // if there is an exception, write it to the output
        if (logEvent.Exception != null)
        {
            output.Write($" | {ToSingleLineString(logEvent.Exception)}");
        }

        output.WriteLine();
    }

    internal static string ToSingleLineString(Exception exception)
    {
        var exceptionString = exception.ToString();

#if NET6_0_OR_GREATER
        return exceptionString.ReplaceLineEndings("\\n");
#else
        return exceptionString.Replace("\r\n", "\\n")
                              .Replace("\n", "\\n");
#endif
    }
}
