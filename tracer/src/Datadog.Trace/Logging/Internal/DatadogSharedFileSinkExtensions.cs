// <copyright file="DatadogSharedFileSinkExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Configuration;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Debugging;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging;

internal static class DatadogSharedFileSinkExtensions
{
    private const int DefaultRetainedFileCountLimit = 31; // A long month of logs

    /// <summary>
    /// Write log events to the specified file using the DatadogSharedFileSink.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="path">Path to the file.</param>
    /// <param name="outputTemplate">A message template describing the format used to write to the sink.</param>
    /// <param name="fileSizeLimitBytes">The approximate maximum size, in bytes, to which a log file will be allowed to grow.
    /// For unrestricted growth, pass null.</param>
    /// <param name="flushToDiskInterval">If provided, a full disk flush will be performed periodically at the specified interval.</param>
    /// <param name="rollingInterval">The interval at which logging will roll over to a new file.</param>
    /// <param name="rollOnFileSizeLimit">If <code>true</code>, a new file will be created when the file size limit is reached. Filenames
    /// will have a number appended in the format <code>_NNN</code>, with the first filename given no number.</param>
    /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
    /// <param name="deferred">Use deferred logger.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration DatadogSharedFile(
        this LoggerSinkConfiguration sinkConfiguration,
        string path,
        string outputTemplate,
        long? fileSizeLimitBytes,
        TimeSpan? flushToDiskInterval = null,
        RollingInterval rollingInterval = RollingInterval.Infinite,
        bool rollOnFileSizeLimit = false,
        Encoding encoding = null,
        bool deferred = false)
    {
        if (sinkConfiguration == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(sinkConfiguration));
        }

        if (path == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(path));
        }

        if (outputTemplate == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(outputTemplate));
        }

        var formatter = new MessageTemplateTextFormatter(outputTemplate);

        ILogEventSink sink;
        if (rollOnFileSizeLimit || rollingInterval != RollingInterval.Infinite)
        {
            sink = new DatadogRollingFileSink(path, formatter, fileSizeLimitBytes, DefaultRetainedFileCountLimit, encoding, rollingInterval, rollOnFileSizeLimit);
        }
        else
        {
            try
            {
                sink = new DatadogSharedFileSink(path, formatter, fileSizeLimitBytes, encoding: encoding);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Unable to open file sink for {0}: {1}", path, ex);
                return sinkConfiguration.Sink(new NullSink(), LevelAlias.Maximum, null);
            }
        }

        if (flushToDiskInterval.HasValue)
        {
#pragma warning disable 618
            sink = new PeriodicFlushToDiskSink(sink, flushToDiskInterval.Value);
#pragma warning restore 618
        }

        if (deferred)
        {
            sink = new DatadogDeferredSink(sink);
        }

        return sinkConfiguration.Sink(sink, LevelAlias.Minimum, null);
    }
}
