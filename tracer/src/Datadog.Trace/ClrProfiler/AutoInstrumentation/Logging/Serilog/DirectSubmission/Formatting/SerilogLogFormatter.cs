// <copyright file="SerilogLogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.Formatting
{
    internal class SerilogLogFormatter
    {
        public static void FormatLogEvent(LogFormatter logFormatter, StringBuilder sb, ILogEvent logEvent)
        {
            var message = logEvent.RenderMessage();

            logFormatter.FormatLog(
                sb,
                logEvent,
                logEvent.Timestamp.UtcDateTime,
                message,
                eventId: null,
                GetLogLevelString(logEvent.Level),
                logEvent.Exception,
                RenderProperties);
        }

#if NET45
        private static string GetLogLevelString(object logLevel) =>
            (int)logLevel switch
            {
                0 => DirectSubmissionLogLevelExtensions.Verbose,
                1 => DirectSubmissionLogLevelExtensions.Debug,
                2 => DirectSubmissionLogLevelExtensions.Information,
                3 => DirectSubmissionLogLevelExtensions.Warning,
                4 => DirectSubmissionLogLevelExtensions.Error,
                5 => DirectSubmissionLogLevelExtensions.Fatal,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
#else
        private static string GetLogLevelString(LogEventLevelDuck logLevel) =>
            logLevel switch
            {
                LogEventLevelDuck.Verbose => DirectSubmissionLogLevelExtensions.Verbose,
                LogEventLevelDuck.Debug => DirectSubmissionLogLevelExtensions.Debug,
                LogEventLevelDuck.Information => DirectSubmissionLogLevelExtensions.Information,
                LogEventLevelDuck.Warning => DirectSubmissionLogLevelExtensions.Warning,
                LogEventLevelDuck.Error => DirectSubmissionLogLevelExtensions.Error,
                LogEventLevelDuck.Fatal => DirectSubmissionLogLevelExtensions.Fatal,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
#endif

        private static LogPropertyRenderingDetails RenderProperties(JsonTextWriter writer, ILogEvent logEvent)
        {
            var haveSource = false;
            var haveService = false;
            var haveHost = false;
            var haveTags = false;
            var haveEnv = false;
            var haveVersion = false;
            foreach (var property in logEvent.Properties)
            {
                var duckProperty = property.DuckCast<KeyValuePairStringStruct>();
                var name = duckProperty.Key;

                haveSource |= LogFormatter.IsSourceProperty(name);
                haveService |= LogFormatter.IsServiceProperty(name);
                haveHost |= LogFormatter.IsHostProperty(name);
                haveTags |= LogFormatter.IsTagsProperty(name);
                haveEnv |= LogFormatter.IsEnvProperty(name);
                haveVersion |= LogFormatter.IsVersionProperty(name);

                LogFormatter.WritePropertyName(writer, name);
                FormatLogEventPropertyValue(writer, duckProperty.Value);
            }

            var renderingDetails = new LogPropertyRenderingDetails(haveSource, haveService, haveHost, haveTags, haveEnv, haveVersion, messageTemplate: logEvent.MessageTemplate.Text);
            return renderingDetails;
        }

        private static void FormatLogEventPropertyValue(JsonTextWriter writer, object value)
        {
            // format the value correctly depending on type
            if (value.TryDuckCast<ScalarValueDuck>(out var scalar))
            {
                LogFormatter.WriteValue(writer, scalar.Value);
                return;
            }

            if (value.TryDuckCast<SequenceValueDuck>(out var sequence))
            {
                FormatSequence(writer, sequence.Elements);
                return;
            }

            if (value.TryDuckCast<StructureValueDuck>(out var structure))
            {
                FormatStructure(writer, structure.Properties, structure.TypeTag);
                return;
            }

            if (value.TryDuckCast<DictionaryValueDuck>(out var dictionary))
            {
                FormatDictionary(writer, dictionary.Elements);
                return;
            }

            throw new InvalidOperationException("Unknown value type " + value.GetType());
        }

        private static void FormatLiteral(JsonWriter writer, object value)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            if (value is string str)
            {
                writer.WriteValue(str);
                return;
            }

            if (value is ValueType)
            {
                switch (value)
                {
                    case int or uint or long or byte or sbyte or short or ushort:
                        writer.WriteValue(Convert.ToInt64(value));
                        return;
                    case ulong ulongValue: // can't safely be cast to long
                        writer.WriteValue(ulongValue);
                        return;
                    case decimal decimalValue:
                        writer.WriteValue(decimalValue);
                        return;
                    case double d:
                        writer.WriteValue(d);
                        return;
                    case float f:
                        writer.WriteValue(f);
                        return;
                    case bool b:
                        writer.WriteValue(b);
                        return;
                    case char c:
                        writer.WriteValue(c);
                        return;
                    case DateTime dt:
                        writer.WriteValue(dt);
                        return;
                    case DateTimeOffset dto:
                        writer.WriteValue(dto);
                        return;
                    case TimeSpan timeSpan:
                        writer.WriteValue(timeSpan);
                        return;
                }
            }

            writer.WriteValue(value.ToString());
        }

        private static void FormatSequence(JsonTextWriter writer, IEnumerable properties)
        {
            writer.WriteStartArray();
            foreach (var property in properties)
            {
                FormatLogEventPropertyValue(writer, property);
            }

            writer.WriteEndArray();
        }

        private static void FormatStructure(JsonTextWriter writer, IEnumerable properties, string typeTag)
        {
            writer.WriteStartObject();

            foreach (var property in properties)
            {
                var duck = property.DuckCast<LogEventPropertyDuck>();
                writer.WritePropertyName(duck.Name);
                FormatLogEventPropertyValue(writer, duck.Value);
            }

            if (!string.IsNullOrEmpty(typeTag))
            {
                writer.WritePropertyName("_typeTag");
                writer.WriteValue(typeTag);
            }

            writer.WriteEndObject();
        }

        private static void FormatDictionary(JsonTextWriter writer, IEnumerable properties)
        {
            writer.WriteStartObject();

            foreach (var property in properties)
            {
                var duck = property.DuckCast<KeyValuePairObjectStruct>();
                writer.WritePropertyName(duck.Key?.ToString() ?? "null");
                FormatLogEventPropertyValue(writer, duck.Value);
            }

            writer.WriteEndObject();
        }
    }
}
