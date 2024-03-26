// <copyright file="LogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Logging.DirectSubmission.Formatting
{
    internal class LogFormatter
    {
        private const string KeyValueTagSeparator = ":";
        private const string TagSeparator = ",";
        private const string SourcePropertyName = "ddsource";
        private const string ServicePropertyName = "service";
        private const string HostPropertyName = "host";
        private const string TagsPropertyName = "ddtags";
        private const string EnvPropertyName = "dd_env";
        private const string VersionPropertyName = "dd_version";

        private readonly string? _source;
        private readonly string? _service;
        private readonly string? _host;
        private readonly string? _env;
        private readonly string? _version;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private bool _gitMetadataAdded;

        private string? _ciVisibilityDdTags;

        public LogFormatter(
            ImmutableTracerSettings settings,
            ImmutableDirectLogSubmissionSettings directLogSettings,
            ImmutableAzureAppServiceSettings? aasSettings,
            string serviceName,
            string env,
            string version,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            _gitMetadataTagsProvider = gitMetadataTagsProvider;
            _source = string.IsNullOrEmpty(directLogSettings.Source) ? null : directLogSettings.Source;
            _service = string.IsNullOrEmpty(serviceName) ? null : serviceName;
            _host = string.IsNullOrEmpty(directLogSettings.Host) ? null : directLogSettings.Host;

            var globalTags = directLogSettings.GlobalTags is { Count: > 0 } ? directLogSettings.GlobalTags : settings.GlobalTagsInternal;

            Tags = EnrichTagsWithAasMetadata(StringifyGlobalTags(globalTags), aasSettings);
            _env = string.IsNullOrEmpty(env) ? null : env;
            _version = string.IsNullOrEmpty(version) ? null : version;
        }

        internal delegate LogPropertyRenderingDetails FormatDelegate<T>(JsonTextWriter writer, in T state);

        internal string? Tags { get; private set; }

        private static string StringifyGlobalTags(IReadOnlyDictionary<string, string> globalTags)
        {
            if (globalTags.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var tagPair in globalTags)
            {
                sb.Append(tagPair.Key)
                  .Append(':')
                  .Append(tagPair.Value)
                  .Append(',');
            }

            // remove final joiner
            return sb.ToString(startIndex: 0, length: sb.Length - 1);
        }

        private string? EnrichTagsWithAasMetadata(string globalTags, ImmutableAzureAppServiceSettings? aasSettings)
        {
            if (aasSettings is null)
            {
                return string.IsNullOrEmpty(globalTags) ? null : globalTags;
            }

            var aasTags = $"{Trace.Tags.AzureAppServicesResourceId}{KeyValueTagSeparator}{aasSettings.ResourceId}";

            return string.IsNullOrEmpty(globalTags) ? aasTags : aasTags + TagSeparator + globalTags;
        }

        private void EnrichTagsStringWithGitMetadata()
        {
            if (_gitMetadataAdded)
            {
                return;
            }

            if (!_gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata))
            {
                // no git tags found, we can try again later
                return;
            }

            if (gitMetadata != GitMetadata.Empty)
            {
                var gitMetadataTags = $"{CommonTags.GitCommit}{KeyValueTagSeparator}{gitMetadata.CommitSha},{CommonTags.GitRepository}{KeyValueTagSeparator}{RemoveScheme(gitMetadata.RepositoryUrl)}";
                Tags = string.IsNullOrEmpty(Tags) ? gitMetadataTags : $"{Tags}{TagSeparator}{gitMetadataTags}";
            }

            _gitMetadataAdded = true;
        }

        private string? RemoveScheme(string url)
        {
            return url switch
            {
                { } when url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) => url.Substring("https://".Length),
                { } when url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) => url.Substring("http://".Length),
                _ => url
            };
        }

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

        internal static JsonTextWriter GetJsonWriter(StringBuilder builder)
        {
            var writer = new JsonTextWriter(new StringWriter(builder));
            writer.Formatting = Vendors.Newtonsoft.Json.Formatting.None;
            return writer;
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
            using var writer = GetJsonWriter(builder);

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

            if (_host is not null && !renderingDetails.HasRenderedHost)
            {
                writer.WritePropertyName(HostPropertyName, escape: false);
                writer.WriteValue(_host);
            }

            EnrichTagsStringWithGitMetadata();

            if (Tags is not null && !renderingDetails.HasRenderedTags)
            {
                writer.WritePropertyName(TagsPropertyName, escape: false);
                writer.WriteValue(Tags);
            }

            writer.WriteEndObject();
        }

        internal void FormatCIVisibilityLog(StringBuilder sb, string source, string? logLevel, string message, ISpan? span)
        {
            using var writer = GetJsonWriter(sb);

            // Based on JsonFormatter
            writer.WriteStartObject();

            writer.WritePropertyName("ddsource", escape: false);
            writer.WriteValue(source);

            if (_host is not null)
            {
                writer.WritePropertyName("hostname", escape: false);
                writer.WriteValue(_host);
            }

            writer.WritePropertyName("timestamp", escape: false);
            writer.WriteValue(DateTimeOffset.UtcNow.ToUnixTimeNanoseconds() / 1_000_000);

            if (logLevel is not null)
            {
                writer.WritePropertyName("status", escape: false);
                writer.WriteValue(logLevel);
            }

            writer.WritePropertyName("message", escape: false);
            writer.WriteValue(message);

            EnrichTagsStringWithGitMetadata();

            var env = _env ?? string.Empty;
            var ddTags = _ciVisibilityDdTags;
            if (ddTags is null)
            {
                ddTags = GetCIVisiblityDDTagsString(env);
                _ciVisibilityDdTags = ddTags;
            }

            var service = _service;
            if (span is not null)
            {
                if (span.GetTag(Trace.Tags.Env) is { } spanEnv && spanEnv != env)
                {
                    ddTags = GetCIVisiblityDDTagsString(spanEnv);
                }

                if (!string.IsNullOrEmpty(span.ServiceName))
                {
                    service = span.ServiceName;
                }

                // encode all 128 bits of the trace id as a hex string, or
                // encode only the lower 64 bits of the trace ids as decimal (not hex)
                writer.WritePropertyName("dd.trace_id", escape: false);
                writer.WriteValue(span.GetTraceIdStringForLogs());

                // 64-bit span ids are always encoded as decimal (not hex)
                writer.WritePropertyName("dd.span_id", escape: false);
                writer.WriteValue(span.SpanId.ToString(CultureInfo.InvariantCulture));

                if (span.GetTag(TestTags.Suite) is { } suite)
                {
                    writer.WritePropertyName(TestTags.Suite, escape: false);
                    writer.WriteValue(suite);
                }

                if (span.GetTag(TestTags.Name) is { } name)
                {
                    writer.WritePropertyName(TestTags.Name, escape: false);
                    writer.WriteValue(name);
                }

                if (span.GetTag(TestTags.Bundle) is { } bundle)
                {
                    writer.WritePropertyName(TestTags.Bundle, escape: false);
                    writer.WriteValue(bundle);
                }
            }

            writer.WritePropertyName("service", escape: false);
            writer.WriteValue(service);

            writer.WritePropertyName("ddtags", escape: false);
            writer.WriteValue(ddTags);

            writer.WriteEndObject();
        }

        private string GetCIVisiblityDDTagsString(string environment)
        {
            // spaces are not allowed inside ddtags
            environment = environment.Replace(" ", string.Empty);
            environment = environment.Replace(":", string.Empty);

            var ddtags = $"env:{environment},datadog.product:citest";
            if (Tags is { Length: > 0 } globalTags)
            {
                ddtags += "," + globalTags;
            }

            return ddtags;
        }
    }
}
