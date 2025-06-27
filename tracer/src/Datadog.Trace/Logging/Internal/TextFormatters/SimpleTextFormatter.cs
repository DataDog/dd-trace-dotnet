// <copyright file="SimpleTextFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;

namespace Datadog.Trace.Logging.Internal.TextFormatters;

internal class SimpleTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        if (logEvent is null)
        {
            return;
        }

        // buffer the log event before writing it to the output
        var sb = StringBuilderCache.Acquire();
        var writer = new StringWriter(sb, output.FormatProvider);

        var utcTimestamp = logEvent.Timestamp.ToUniversalTime();
        var logLevel = LevelOutputFormat.GetLevelMoniker(logEvent.Level, "u3");
        writer.Write($"[{utcTimestamp:yyyy-MM-dd HH:mm:ss.fff zzz} | DD_TRACE_DOTNET | {logLevel}] ");

        // render the message using the template and properties
        logEvent.MessageTemplate.Render(logEvent.Properties, writer);

        if (logEvent.Exception != null)
        {
            writer.Write($" | {logEvent.Exception}");
        }

        // replace any newlines
        sb.Replace(Environment.NewLine, "\\n");
        output.WriteLine(sb);

        StringBuilderCache.Release(sb);
    }
}
