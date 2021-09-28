// <copyright file="LoggerLogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting
{
    internal class LoggerLogFormatter
    {
        private const string MessageTemplateKey = "{OriginalFormat}";

        public static string FormatLogEvent<T>(LogFormatter logFormatter, in LogEntry<T> logEntry)
        {
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                // No message! weird...
                return null;
            }

            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            logFormatter.FormatLog(
                sb,
                logEntry,
                logEntry.Timestamp,
                message,
                logEntry.EventId == 0 ? null : logEntry.EventId,
                GetLogLevelString(logEntry.LogLevel),
                logEntry.Exception,
                (writer, log) => RenderProperties(writer, in log));

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static LogPropertyRenderingDetails RenderProperties<T>(JsonTextWriter writer, in LogEntry<T> logEntry)
        {
            var haveSource = false;
            var haveService = false;
            var haveHost = false;
            var haveTags = false;
            string messageTemplate = null;

            if (logEntry.State is { } state)
            {
                if (state is IEnumerable<KeyValuePair<string, object>> stateProperties)
                {
                    foreach (var item in stateProperties)
                    {
                        // we don't need to write the message template
                        // (but should we) ?
                        var key = item.Key;
                        if (key != MessageTemplateKey)
                        {
                            haveSource = haveSource || LogFormatter.IsSourceProperty(key);
                            haveService = haveService || LogFormatter.IsServiceProperty(key);
                            haveHost = haveHost || LogFormatter.IsHostProperty(key);
                            haveTags = haveTags || LogFormatter.IsTagsProperty(key);

                            writer.WritePropertyName(key);
                            LogFormatter.WriteValue(writer, item.Value);
                        }
                        else if (item.Value is string stringValue)
                        {
                            messageTemplate = stringValue;
                        }
                    }
                }
                else
                {
                    writer.WritePropertyName("@s", escape: false);
                    LogFormatter.WriteValue(writer, state);
                }
            }

            if (logEntry.ScopeProvider is { } scopeProvider)
            {
                var writerWrapper = new WriterWrapper(writer);
                scopeProvider.ForEachScope(
                    (scope, wrapper) =>
                    {
                        // Add dictionary scopes to the "root" object
                        // TODO: Need to handle "duplicate" values, where the property has been added as state,
                        // And then we try and add it again
                        if (scope is IEnumerable<KeyValuePair<string, object>> scopeItems)
                        {
                            foreach (var item in scopeItems)
                            {
                                var key = item.Key;
                                haveSource = haveSource || LogFormatter.IsSourceProperty(key);
                                haveService = haveService || LogFormatter.IsServiceProperty(key);
                                haveHost = haveHost || LogFormatter.IsHostProperty(key);
                                haveTags = haveTags || LogFormatter.IsTagsProperty(key);

                                wrapper.Writer.WritePropertyName(key);
                                LogFormatter.WriteValue(wrapper.Writer, item.Value);
                            }
                        }
                        else
                        {
                            wrapper.Values.Add(scope); // add to list for inclusion in scope array
                        }
                    },
                    writerWrapper);

                if (writerWrapper.Values.Count > 0)
                {
                    writer.WritePropertyName("Scopes");
                    writer.WriteStartArray();

                    for (var i = 0; i < writerWrapper.Values.Count; i++)
                    {
                        LogFormatter.WriteValue(writer, writerWrapper.Values[i]);
                    }

                    writer.WriteEndArray();
                }
            }

            return new LogPropertyRenderingDetails(haveSource, haveService, haveHost, haveTags, messageTemplate);
        }

        private static string GetLogLevelString(int logLevel) =>
            logLevel switch
            {
                0 => DirectSubmissionLogLevelExtensions.Verbose, // Trace
                1 => DirectSubmissionLogLevelExtensions.Debug,
                2 => DirectSubmissionLogLevelExtensions.Information,
                3 => DirectSubmissionLogLevelExtensions.Warning,
                4 => DirectSubmissionLogLevelExtensions.Error,
                5 => DirectSubmissionLogLevelExtensions.Fatal, // Critical
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };

        private readonly struct WriterWrapper
        {
            public readonly JsonWriter Writer;
            public readonly List<object> Values;

            public WriterWrapper(JsonWriter writer)
            {
                Writer = writer;
                Values = new List<object>();
            }
        }
    }
}
