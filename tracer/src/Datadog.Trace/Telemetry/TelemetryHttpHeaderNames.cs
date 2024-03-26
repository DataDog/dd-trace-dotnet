// <copyright file="TelemetryHttpHeaderNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry
{
    internal static class TelemetryHttpHeaderNames
    {
        /// <summary>
        /// Gets the default constant headers that should be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultAgentHeaders()
            => new[]
            {
                new KeyValuePair<string, string>(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language),
                new KeyValuePair<string, string>(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.AssemblyVersion),
                new KeyValuePair<string, string>(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
            };

        /// <summary>
        /// Gets the default constant headers that should be added to any request to the direct telemetry intake
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultIntakeHeaders(TelemetrySettings.AgentlessSettings settings)
        {
            var headerCount = settings.Cloud is null ? 4 : 7;

            var headers = new KeyValuePair<string, string>[headerCount];

            headers[0] = new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language);
            headers[1] = new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.AssemblyVersion);
            headers[2] = new(HttpHeaderNames.TracingEnabled, "false"); // don't add automatic instrumentation to requests directed to the agent
            headers[3] = new(TelemetryConstants.ApiKeyHeader, settings.ApiKey);

            if (settings.Cloud is { } cloud)
            {
                headers[4] = new(TelemetryConstants.CloudProviderHeader, cloud.Provider);
                headers[5] = new(TelemetryConstants.CloudResourceTypeHeader, cloud.ResourceType);
                headers[6] = new(TelemetryConstants.CloudResourceIdentifierHeader, cloud.ResourceIdentifier);
            }

            return headers;
        }
    }
}
