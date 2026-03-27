// <copyright file="TelemetryHttpHeaderNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry
{
    internal static class TelemetryHttpHeaderNames
    {
        /// <summary>
        /// Gets the default agent headers in the format <c>Key: Value\r\n</c>. For use in HTTP headers.
        /// Not a const because session headers depend on runtime values.
        /// </summary>
        internal static string HttpSerializedDefaultAgentHeaders { get; } = BuildSerializedAgentHeaders();

        /// <summary>
        /// Gets the default constant headers that should be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultAgentHeaders()
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language),
                new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.AssemblyVersion),
                new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
                new(TelemetryConstants.SessionIdHeader, RuntimeId.Get()),
            };

            AddRootSessionIdHeader(headers);

            return headers.ToArray();
        }

        /// <summary>
        /// Gets the default constant headers that should be added to any request to the direct telemetry intake
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultIntakeHeaders(TelemetrySettings.AgentlessSettings settings)
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language),
                new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.AssemblyVersion),
                new(HttpHeaderNames.TracingEnabled, "false"),
                new(TelemetryConstants.ApiKeyHeader, settings.ApiKey),
                new(TelemetryConstants.SessionIdHeader, RuntimeId.Get()),
            };

            if (settings.Cloud is { } cloud)
            {
                headers.Add(new(TelemetryConstants.CloudProviderHeader, cloud.Provider));
                headers.Add(new(TelemetryConstants.CloudResourceTypeHeader, cloud.ResourceType));
                headers.Add(new(TelemetryConstants.CloudResourceIdentifierHeader, cloud.ResourceIdentifier));
            }

            AddRootSessionIdHeader(headers);

            return headers.ToArray();
        }

        private static void AddRootSessionIdHeader(List<KeyValuePair<string, string>> headers)
        {
            var sessionId = RuntimeId.Get();
            var rootSessionId = RuntimeId.GetRootSessionId();
            if (rootSessionId != sessionId)
            {
                headers.Add(new(TelemetryConstants.RootSessionIdHeader, rootSessionId));
            }
        }

        private static string BuildSerializedAgentHeaders()
        {
            var serialized =
                $"{TelemetryConstants.ClientLibraryLanguageHeader}: {TracerConstants.Language}" + DatadogHttpValues.CrLf +
                $"{TelemetryConstants.ClientLibraryVersionHeader}: {TracerConstants.AssemblyVersion}" + DatadogHttpValues.CrLf +
                $"{HttpHeaderNames.TracingEnabled}: false" + DatadogHttpValues.CrLf +
                $"{TelemetryConstants.SessionIdHeader}: {RuntimeId.Get()}" + DatadogHttpValues.CrLf;

            var sessionId = RuntimeId.Get();
            var rootSessionId = RuntimeId.GetRootSessionId();
            if (rootSessionId != sessionId)
            {
                serialized += $"{TelemetryConstants.RootSessionIdHeader}: {rootSessionId}" + DatadogHttpValues.CrLf;
            }

            return serialized;
        }
    }
}
