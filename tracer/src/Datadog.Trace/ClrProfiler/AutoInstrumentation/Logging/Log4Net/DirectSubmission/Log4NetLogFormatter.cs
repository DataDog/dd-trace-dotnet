// <copyright file="Log4NetLogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    internal class Log4NetLogFormatter
    {
        public static void FormatLogEvent(LogFormatter logFormatter, StringBuilder sb, LoggingEventDuck logEntry)
        {
            logFormatter.FormatLog(
                sb,
                logEntry,
                logEntry.TimeStampUtc,
                logEntry.RenderedMessage,
                eventId: null,
                logEntry.Level.ToStandardLevelString(),
                logEntry.ExceptionObject,
                RenderProperties);
        }

        private static LogPropertyRenderingDetails RenderProperties(JsonTextWriter writer, LoggingEventDuck logEntry)
        {
            var haveSource = false;
            var haveService = false;
            var haveHost = false;
            var haveTags = false;

            var properties = logEntry.GetProperties();
            foreach (var keyObj in properties.Keys)
            {
                var name = keyObj as string;
                if (name is null || name.StartsWith("log4net:"))
                {
                    continue;
                }

                var value = properties[name];

                switch (value)
                {
                    case MethodInfo _:
                    case Assembly _:
                    case Module _:
                        continue;
                    default:
                        haveSource |= LogFormatter.IsSourceProperty(name);
                        haveService |= LogFormatter.IsServiceProperty(name);
                        haveHost |= LogFormatter.IsHostProperty(name);
                        haveTags |= LogFormatter.IsTagsProperty(name);

                        if (name.Length > 0 && name[0] == '@')
                        {
                            // Escape first '@' by doubling
                            name = '@' + name;
                        }

                        writer.WritePropertyName(name);
                        writer.WriteValue(value);
                        break;
                }
            }

            return new LogPropertyRenderingDetails(haveSource, haveService, haveHost, haveTags, messageTemplate: logEntry.Message);
        }
    }
}
