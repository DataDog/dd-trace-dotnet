// <copyright file="LogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Logging.DirectSubmission.Formatting
{
    internal class LogFormatter
    {
        private const string SourcePropertyName = "ddsource";
        private const string ServicePropertyName = "ddservice";
        private const string HostPropertyName = "host";
        private const string TagsPropertyName = "ddtags";
        private const string EnvPropertyName = "dd_env";
        private const string VersionPropertyName = "dd_version";

        private readonly string? _source;
        private readonly string? _service;
        private readonly string? _host;
        private readonly string? _globalTags;
        private readonly string? _env;
        private readonly string? _version;

        public LogFormatter(
            ImmutableDirectLogSubmissionSettings settings,
            string serviceName,
            string env,
            string version)
        {
            _source = string.IsNullOrEmpty(settings.Source) ? null : settings.Source;
            _service = string.IsNullOrEmpty(serviceName) ? null : serviceName;
            _host = string.IsNullOrEmpty(settings.Host) ? null : settings.Host;
            _globalTags = string.IsNullOrEmpty(settings.GlobalTags) ? null : settings.GlobalTags;
            _env = string.IsNullOrEmpty(env) ? null : env;
            _version = string.IsNullOrEmpty(version) ? null : version;
        }

        internal delegate LogPropertyRenderingDetails FormatDelegate<T>(JsonTextWriter writer, in T state);

        internal static bool IsSourceProperty(string? propertyName) =>
            string.Equals(propertyName, SourcePropertyName, StringComparison.OrdinalIgnoreCase);

        internal static bool IsServiceProperty(string? propertyName) =>
            string.Equals(propertyName, ServicePropertyName, StringComparison.OrdinalIgnoreCase)
         || string.Equals(propertyName, "dd_service", StringComparison.OrdinalIgnoreCase)
         || string.Equals(propertyName, "dd.service", StringComparison.OrdinalIgnoreCase);

        internal static bool IsHostProperty(string? propertyName) =>
            string.Equals(propertyName, HostPropertyName, StringComparison.OrdinalIgnoreCase);

        internal static bool IsTagsProperty(string? propertyName) =>
            string.Equals(propertyName, TagsPropertyName, StringComparison.OrdinalIgnoreCase);

        internal static bool IsEnvProperty(string? propertyName) =>
            string.Equals(propertyName, EnvPropertyName, StringComparison.OrdinalIgnoreCase)
         || string.Equals(propertyName, "dd.env", StringComparison.OrdinalIgnoreCase);

        internal static bool IsVersionProperty(string? propertyName) =>
            string.Equals(propertyName, VersionPropertyName, StringComparison.OrdinalIgnoreCase)
         || string.Equals(propertyName, "dd.version", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Helper for writing property names in JSON
        /// </summary>
        internal static void WritePropertyName(JsonWriter writer, string name)
        {
            if (name.Length > 0 && name[0] == '@')
            {
                // Escape first '@' by doubling
                name = '@' + name;
            }

            writer.WritePropertyName(name);
        }

        /// <summary>
        /// Helper for writing values as JSON
        /// </summary>
        internal static void WriteValue(JsonWriter writer, object? value)
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

            writer.WriteValue(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Format the log, based on <see cref="Datadog.Trace.Vendors.Serilog.Formatting.Json.JsonFormatter"/>
        /// and CompactFormatter
        /// </summary>
        /// <typeparam name="T">The type of state being formatted</typeparam>
        internal void FormatLog<T>(
            StringBuilder builder,
            in T state,
            DateTime timestamp,
            string message,
            int? eventId,
            string logLevel,
            Exception? exception,
            FormatDelegate<T> renderPropertiesDelegate)
        {
            var sw = new StringWriter(builder);
            using var writer = new JsonTextWriter(sw);
            writer.Formatting = Vendors.Newtonsoft.Json.Formatting.None;

            // Based on JsonFormatter
            writer.WriteStartObject();

            writer.WritePropertyName("@t", escape: false);
            writer.WriteValue(timestamp.ToString("O"));

            writer.WritePropertyName("@m", escape: false);
            writer.WriteValue(message);

            if (eventId.HasValue)
            {
                writer.WritePropertyName("@i", escape: false);
                writer.WriteValue(eventId);
            }

            if (logLevel != "Information")
            {
                writer.WritePropertyName("@l", escape: false);
                writer.WriteValue(logLevel);
            }

            if (exception != null)
            {
                writer.WritePropertyName("@x", escape: false);
                writer.WriteValue(exception.ToString());
            }

            var renderingDetails = renderPropertiesDelegate(writer, in state);

            if (!eventId.HasValue && !string.IsNullOrEmpty(renderingDetails.MessageTemplate))
            {
                // compute an eventID based on the message template (smaller than using the message template itself)
                var id = EventIdHash.Compute(renderingDetails.MessageTemplate!); // we already checked it's not null
                writer.WritePropertyName("@i", escape: false);
                writer.WriteValue(id.ToString("x8"));
            }

            // add ddTrace values (if not already added, and not null)
            if (_source is not null && !renderingDetails.HasRenderedSource)
            {
                writer.WritePropertyName(SourcePropertyName, escape: false);
                writer.WriteValue(_source);
            }

            if (_service is not null && !renderingDetails.HasRenderedService)
            {
                writer.WritePropertyName(ServicePropertyName, escape: false);
                writer.WriteValue(_service);
            }

            if (_env is not null && !renderingDetails.HasRenderedEnv)
            {
                writer.WritePropertyName(EnvPropertyName, escape: false);
                writer.WriteValue(_env);
            }

            if (_version is not null && !renderingDetails.HasRenderedVersion)
            {
                writer.WritePropertyName(VersionPropertyName, escape: false);
                writer.WriteValue(_version);
            }

            if (_host is not null && !renderingDetails.HasRenderedSource)
            {
                writer.WritePropertyName(HostPropertyName, escape: false);
                writer.WriteValue(_host);
            }

            if (_globalTags is not null && !renderingDetails.HasRenderedTags)
            {
                writer.WritePropertyName(TagsPropertyName, escape: false);
                writer.WriteValue(_globalTags);
            }

            writer.WriteEndObject();
        }
    }
}
