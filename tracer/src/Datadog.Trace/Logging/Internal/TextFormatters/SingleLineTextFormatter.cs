// <copyright file="SingleLineTextFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;

namespace Datadog.Trace.Logging.Internal.TextFormatters;

internal class SingleLineTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        // buffer the entire log event into a single line before writing it to the output
        var buffer = StringBuilderCache.Acquire();
        var bufferWriter = new StringWriter(buffer, output.FormatProvider);

        var utcTimestamp = logEvent.Timestamp.ToUniversalTime();
        var logLevel = LevelOutputFormat.GetLevelMoniker(logEvent.Level, "u3");
        bufferWriter.Write($"[{utcTimestamp:yyyy-MM-dd HH:mm:ss.fff zzz} | DD_TRACE_DOTNET | {logLevel}] ");

        // write the message to the output, using the template and properties
        logEvent.RenderMessage(bufferWriter, output.FormatProvider);

        // if there is an exception, write it to the output
        if (logEvent.Exception != null)
        {
            bufferWriter.Write($" | {logEvent.Exception}");
        }

        // replace any newlines with literal "\n" to ensure the output is a single line
        buffer.Replace("\r\n", "\\n")
              .Replace("\n", "\\n");

        // We intentionally pass the StringBuilder directly to the TextWriter:
        // - In newer runtimes, this calls the new TextWriter.Write(StringBuilder) overload which
        //   uses StringBuilder.GetChunks() internally without allocating a new string.
        // - In older runtimes, this calls the TextWriter.Write(object) overload, which calls StringBuilder.ToString() internally.
        output.WriteLine(buffer);
        StringBuilderCache.Release(buffer);
    }
}
