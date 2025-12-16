// <copyright file="LogFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Logging.DirectSubmission.Formatting
{
    internal sealed class LogFormatter : IDisposable
    {
        private const char KeyValueTagSeparator = ':';
        private const char TagSeparator = ',';
        private const string SourcePropertyName = "ddsource";
        private const string ServicePropertyName = "service";
        private const string HostPropertyName = "host";
        private const string TagsPropertyName = "ddtags";
        private const string EnvPropertyName = "dd_env";
        private const string VersionPropertyName = "dd_version";

        private readonly object _lock = new();
        private readonly IDisposable _settingSub;
        private readonly string? _source;
        private readonly string? _host;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly bool _use128Bits;

        private string? _gitMetadataTags;
        private string? _ciVisibilityDdTags;
        private ServiceTags _serviceTags;

        public LogFormatter(
            TracerSettings settings,
            DirectLogSubmissionSettings directLogSettings,
            ImmutableAzureAppServiceSettings? aasSettings,
            IGitMetadataTagsProvider gitMetadataTagsProvider)
        {
            _source = string.IsNullOrEmpty(directLogSettings.Source) ? null : directLogSettings.Source;
            _host = string.IsNullOrEmpty(directLogSettings.Host) ? null : directLogSettings.Host;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;
            _use128Bits = settings.TraceId128BitLoggingEnabled;

            UpdateServiceTags(settings.Manager.InitialMutableSettings);
            _settingSub = settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } mutable)
                {
                    UpdateServiceTags(mutable);
                }
            });

            [MemberNotNull(nameof(_serviceTags))]
            void UpdateServiceTags(MutableSettings mutableSettings)
            {
                // we take a lock here to handle the case where we're running
                // concurrently with EnrichTagsStringWithGitMetadata
                lock (_lock)
                {
                    var service = mutableSettings.DefaultServiceName;
                    var env = string.IsNullOrEmpty(mutableSettings.Environment) ? null : mutableSettings.Environment;
                    var version = string.IsNullOrEmpty(mutableSettings.ServiceVersion) ? null : mutableSettings.ServiceVersion;
                    var tagDictionary = directLogSettings.GlobalTags is { Count: > 0 } ? directLogSettings.GlobalTags : mutableSettings.GlobalTags;
                    var globalTags = StringifyGlobalTags(tagDictionary, aasSettings);
                    var gitMetadataTags = _gitMetadataTags;
                    _serviceTags = new ServiceTags(service, env, version, JoinTags(globalTags, gitMetadataTags));
                }
            }
        }

        internal delegate LogPropertyRenderingDetails FormatDelegate<T>(JsonTextWriter writer, in T state);

        // Internal for testing only
        internal string? Tags => Volatile.Read(ref _serviceTags).Tags;

        private static string StringifyGlobalTags(
            IReadOnlyDictionary<string, string> globalTags,
            ImmutableAzureAppServiceSettings? aasSettings)
        {
            var hasResourceId = !string.IsNullOrEmpty(aasSettings?.ResourceId);
            var hasSiteKind = !string.IsNullOrEmpty(aasSettings?.SiteKind);
            if (globalTags.Count == 0 && !hasResourceId && !hasSiteKind)
            {
                return string.Empty;
            }

            var sb = StringBuilderCache.Acquire();

            // AAS tags
            if (hasResourceId)
            {
                sb.Append(Trace.Tags.AzureAppServicesResourceId)
                  .Append(KeyValueTagSeparator)
                  .Append(aasSettings?.ResourceId)
                  .Append(TagSeparator);
            }

            if (hasSiteKind)
            {
                sb.Append(Trace.Tags.AzureAppServicesSiteKind)
                  .Append(KeyValueTagSeparator)
                  .Append(aasSettings?.SiteKind)
                  .Append(TagSeparator);
            }

            foreach (var tagPair in globalTags)
            {
                sb.Append(tagPair.Key)
                  .Append(KeyValueTagSeparator)
                  .Append(tagPair.Value)
                  .Append(TagSeparator);
            }

            // remove final joiner
            sb.Remove(sb.Length - 1, length: 1);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static string RemoveScheme(string url)
        {
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring("https://".Length);
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring("http://".Length);
            }

            return url;
        }

        private static string? JoinTags(string? globalTags, string? gitMetadataTags)
        {
            if (StringUtil.IsNullOrEmpty(gitMetadataTags))
            {
                return globalTags;
            }

            return StringUtil.IsNullOrEmpty(globalTags) ? gitMetadataTags : $"{globalTags}{TagSeparator}{gitMetadataTags}";
        }

        private void EnrichTagsStringWithGitMetadata()
        {
            if (_gitMetadataTags is not null)
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
                // we take a lock here to handle the case where we're running concurrently with a settings update
                var gitMetadataTags = $"{CommonTags.GitCommit}{KeyValueTagSeparator}{gitMetadata.CommitSha},{CommonTags.GitRepository}{KeyValueTagSeparator}{RemoveScheme(gitMetadata.RepositoryUrl)}";
                Volatile.Write(ref _gitMetadataTags, gitMetadataTags);
                lock (_lock)
                {
                    var currentServiceTags = _serviceTags;
                    _serviceTags = currentServiceTags with { Tags = JoinTags(currentServiceTags.Tags, gitMetadataTags) };
                }
            }
            else
            {
                Volatile.Write(ref _gitMetadataTags, string.Empty); // to signal that we extracted it but it was missing
            }
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

            EnrichTagsStringWithGitMetadata();
            var serviceTags = _serviceTags;

            if (serviceTags.Service is not null && !renderingDetails.HasRenderedService)
            {
                writer.WritePropertyName(ServicePropertyName, escape: false);
                writer.WriteValue(serviceTags.Service);
            }

            if (serviceTags.Env is not null && !renderingDetails.HasRenderedEnv)
            {
                writer.WritePropertyName(EnvPropertyName, escape: false);
                writer.WriteValue(serviceTags.Env);
            }

            if (serviceTags.Version is not null && !renderingDetails.HasRenderedVersion)
            {
                writer.WritePropertyName(VersionPropertyName, escape: false);
                writer.WriteValue(serviceTags.Version);
            }

            if (_host is not null && !renderingDetails.HasRenderedHost)
            {
                writer.WritePropertyName(HostPropertyName, escape: false);
                writer.WriteValue(_host);
            }

            if (!StringUtil.IsNullOrEmpty(serviceTags.Tags) && !renderingDetails.HasRenderedTags)
            {
                writer.WritePropertyName(TagsPropertyName, escape: false);
                writer.WriteValue(serviceTags.Tags);
            }

            writer.WriteEndObject();
        }

        internal void FormatCIVisibilityLog(StringBuilder sb, string source, string? logLevel, string message, Span? span)
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
            var serviceTags = _serviceTags;

            var env = serviceTags.Env ?? string.Empty;
            var ddTags = _ciVisibilityDdTags;
            if (ddTags is null)
            {
                ddTags = GetCIVisiblityDDTagsString(serviceTags, env);
                _ciVisibilityDdTags = ddTags;
            }

            var service = serviceTags.Service;
            if (span is not null)
            {
                if (span.GetTag(Trace.Tags.Env) is { } spanEnv && spanEnv != env)
                {
                    ddTags = GetCIVisiblityDDTagsString(serviceTags, spanEnv);
                }

                if (!string.IsNullOrEmpty(span.ServiceName))
                {
                    service = span.ServiceName;
                }

                if (LogContext.TryGetValues(span.Context, out var traceId, out var spanId, _use128Bits))
                {
                    writer.WritePropertyName("dd.trace_id", escape: false);
                    writer.WriteValue(traceId);

                    writer.WritePropertyName("dd.span_id", escape: false);
                    writer.WriteValue(spanId);
                }

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

        private string GetCIVisiblityDDTagsString(ServiceTags serviceTags, string environment)
        {
            // spaces are not allowed inside ddtags
            environment = environment.Replace(" ", string.Empty);
            environment = environment.Replace(":", string.Empty);

            var ddtags = $"env:{environment},datadog.product:citest";
            if (serviceTags.Tags is { Length: > 0 } globalTags)
            {
                ddtags += "," + globalTags;
            }

            return ddtags;
        }

        public void Dispose() => _settingSub.Dispose();

        private sealed record ServiceTags(string? Service, string? Env, string? Version, string? Tags);
    }
}
