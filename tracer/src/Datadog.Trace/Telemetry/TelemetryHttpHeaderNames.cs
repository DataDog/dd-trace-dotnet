// <copyright file="TelemetryHttpHeaderNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry
{
    internal static class TelemetryHttpHeaderNames
    {
        private static string _httpSerializedDefaultAgentHeaders;

        /// <summary>
        /// Gets the default agent headers in the format <c>Key: Value\r\n</c>. For use in HTTP headers.
        /// Lazily initialized because session headers depend on runtime values.
        /// </summary>
        internal static string HttpSerializedDefaultAgentHeaders =>
            LazyInitializer.EnsureInitialized(ref _httpSerializedDefaultAgentHeaders, BuildSerializedAgentHeaders);

        /// <summary>
        /// Gets the default constant headers that should be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultAgentHeaders()
        {
            var sessionId = RuntimeId.Get();
            var rootSessionId = RuntimeId.GetRootSessionId();
            var includeRoot = rootSessionId != sessionId;
            var headerCount = includeRoot ? 5 : 4;

            var headers = new KeyValuePair<string, string>[headerCount];
            headers[0] = new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language);
            headers[1] = new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.AssemblyVersion);
            headers[2] = new(HttpHeaderNames.TracingEnabled, "false"); // don't add automatic instrumentation to requests directed to the agent
            headers[3] = new(TelemetryConstants.SessionIdHeader, sessionId);

            if (includeRoot)
            {
                headers[4] = new(TelemetryConstants.RootSessionIdHeader, rootSessionId);
            }

            return headers;
        }

        /// <summary>
        /// Gets the default constant headers that should be added to any request to the direct telemetry intake
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultIntakeHeaders(TelemetrySettings.AgentlessSettings settings)
        {
            var sessionId = RuntimeId.Get();
            var rootSessionId = RuntimeId.GetRootSessionId();
            var includeRoot = rootSessionId != sessionId;
            var baseCount = settings.Cloud is null ? 5 : 8;
            var headerCount = includeRoot ? baseCount + 1 : baseCount;

            var headers = new KeyValuePair<string, string>[headerCount];
            headers[0] = new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language);
            headers[1] = new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.AssemblyVersion);
            headers[2] = new(HttpHeaderNames.TracingEnabled, "false");
            headers[3] = new(TelemetryConstants.ApiKeyHeader, settings.ApiKey);
            headers[4] = new(TelemetryConstants.SessionIdHeader, sessionId);

            var index = 5;
            if (settings.Cloud is { } cloud)
            {
                headers[index++] = new(TelemetryConstants.CloudProviderHeader, cloud.Provider);
                headers[index++] = new(TelemetryConstants.CloudResourceTypeHeader, cloud.ResourceType);
                headers[index++] = new(TelemetryConstants.CloudResourceIdentifierHeader, cloud.ResourceIdentifier);
            }

            if (includeRoot)
            {
                headers[index] = new(TelemetryConstants.RootSessionIdHeader, rootSessionId);
            }

            return headers;
        }

        private static string BuildSerializedAgentHeaders()
        {
            var sessionId = RuntimeId.Get();
            var rootSessionId = RuntimeId.GetRootSessionId();

            if (rootSessionId != sessionId)
            {
                return $"{TelemetryConstants.ClientLibraryLanguageHeader}: {TracerConstants.Language}" + DatadogHttpValues.CrLf +
                    $"{TelemetryConstants.ClientLibraryVersionHeader}: {TracerConstants.AssemblyVersion}" + DatadogHttpValues.CrLf +
                    $"{HttpHeaderNames.TracingEnabled}: false" + DatadogHttpValues.CrLf +
                    $"{TelemetryConstants.SessionIdHeader}: {sessionId}" + DatadogHttpValues.CrLf +
                    $"{TelemetryConstants.RootSessionIdHeader}: {rootSessionId}" + DatadogHttpValues.CrLf;
            }

            return $"{TelemetryConstants.ClientLibraryLanguageHeader}: {TracerConstants.Language}" + DatadogHttpValues.CrLf +
                $"{TelemetryConstants.ClientLibraryVersionHeader}: {TracerConstants.AssemblyVersion}" + DatadogHttpValues.CrLf +
                $"{HttpHeaderNames.TracingEnabled}: false" + DatadogHttpValues.CrLf +
                $"{TelemetryConstants.SessionIdHeader}: {sessionId}" + DatadogHttpValues.CrLf;
        }
    }
}
