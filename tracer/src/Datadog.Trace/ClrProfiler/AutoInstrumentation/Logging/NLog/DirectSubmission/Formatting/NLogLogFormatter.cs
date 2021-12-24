// <copyright file="NLogLogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting
{
    internal class NLogLogFormatter
    {
        public static string FormatLogEvent(LogFormatter logFormatter, in LogEntry logEntryWrapper)
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            FormatLogEvent(logFormatter, sb, logEntryWrapper);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public static void FormatLogEvent(LogFormatter logFormatter, StringBuilder sb, in LogEntry logEntryWrapper)
        {
            var logEntry = logEntryWrapper.LogEventInfo;
            logFormatter.FormatLog(
                sb,
                logEntryWrapper,
                logEntry.TimeStamp.ToUniversalTime(),
                logEntry.FormattedMessage,
                eventId: null,
                GetLogLevelString(logEntry.Level),
                logEntry.Exception,
                RenderProperties);
        }

        private static LogPropertyRenderingDetails RenderProperties(JsonTextWriter writer, in LogEntry logEntryWrapper)
        {
            var haveSource = false;
            var haveService = false;
            var haveHost = false;
            var haveTags = false;
            var haveEnv = false;
            var haveVersion = false;

            if (logEntryWrapper.Properties is not null)
            {
                foreach (var kvp in logEntryWrapper.Properties)
                {
                    var name = kvp.Key;
                    haveSource |= LogFormatter.IsSourceProperty(name);
                    haveService |= LogFormatter.IsServiceProperty(name);
                    haveHost |= LogFormatter.IsHostProperty(name);
                    haveTags |= LogFormatter.IsTagsProperty(name);
                    haveEnv |= LogFormatter.IsEnvProperty(name);
                    haveVersion |= LogFormatter.IsVersionProperty(name);

                    LogFormatter.WritePropertyName(writer, name);
                    LogFormatter.WriteValue(writer, kvp.Value);
                }
            }
            else if (logEntryWrapper.FallbackProperties is not null)
            {
                foreach (var kvp in logEntryWrapper.FallbackProperties)
                {
                    var name = kvp.Key.ToString();
                    haveSource |= LogFormatter.IsSourceProperty(name);
                    haveService |= LogFormatter.IsServiceProperty(name);
                    haveHost |= LogFormatter.IsHostProperty(name);
                    haveTags |= LogFormatter.IsTagsProperty(name);
                    haveEnv |= LogFormatter.IsEnvProperty(name);
                    haveVersion |= LogFormatter.IsVersionProperty(name);

                    LogFormatter.WritePropertyName(writer, name);
                    LogFormatter.WriteValue(writer, kvp.Value);
                }
            }

            return new LogPropertyRenderingDetails(haveSource, haveService, haveHost, haveTags, haveEnv, haveVersion, messageTemplate: logEntryWrapper.LogEventInfo.Message);
        }

        private static string GetLogLevelString(LogLevelProxy logLevel) =>
            logLevel.Ordinal switch
            {
                0 => DirectSubmissionLogLevelExtensions.Verbose,
                1 => DirectSubmissionLogLevelExtensions.Debug,
                2 => DirectSubmissionLogLevelExtensions.Information,
                3 => DirectSubmissionLogLevelExtensions.Warning,
                4 => DirectSubmissionLogLevelExtensions.Error,
                5 => DirectSubmissionLogLevelExtensions.Fatal,
                // Technically there's a 6, off, but should never have this level in a log message
                _ => DirectSubmissionLogLevelExtensions.Unknown,
            };
    }
}
