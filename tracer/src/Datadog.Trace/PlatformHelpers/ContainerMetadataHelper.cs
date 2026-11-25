// <copyright file="ContainerMetadataHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers;

internal static class ContainerMetadataHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContainerMetadataHelper));

    /// <summary>
    /// Maximum length of the <c>DD_EXTERNAL_ENV</c> value that is forwarded in the <c>Datadog-External-Env</c> header.
    /// Longer values are ignored, as they could cause the whole HTTP request to fail.
    /// </summary>
    internal const int MaxExternalEnvLength = 1024;

    public static void AddContainerMetadataHeaders(this IApiRequest request, ContainerMetadata containerMetadata)
    {
        // Set additional headers
        if (containerMetadata.ContainerId is not null)
        {
            request.AddHeader(AgentHttpHeaderNames.ContainerId, containerMetadata.ContainerId);
        }

        if (containerMetadata.EntityId is not null)
        {
            request.AddHeader(AgentHttpHeaderNames.EntityId, containerMetadata.EntityId);
        }

        if (containerMetadata.ExternalEnv is not null)
        {
            request.AddHeader(AgentHttpHeaderNames.ExternalEnv, containerMetadata.ExternalEnv);
        }
    }

    /// <summary>
    /// Reads the origin detection information injected by the Datadog Cluster Agent Admission Controller
    /// from the <c>DD_EXTERNAL_ENV</c> environment variable.
    /// </summary>
    /// <returns>The value to send in the <see cref="AgentHttpHeaderNames.ExternalEnv"/> header, or <c>null</c> if not available.</returns>
    internal static string? GetExternalEnvFromEnvironment()
        => NormalizeExternalEnv(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ExternalEnv));

    /// <summary>
    /// The value is forwarded to the agent unparsed, but we only trim surrounding whitespace and reject values
    /// that can't be sent in an HTTP header (including values that are too long),
    /// as adding an invalid header value would cause the whole request to fail.
    /// </summary>
    /// <param name="value">The raw value of the <c>DD_EXTERNAL_ENV</c> environment variable</param>
    /// <returns>The value to send in the header, or <c>null</c> if the header should not be sent.</returns>
    internal static string? NormalizeExternalEnv(string? value)
    {
        if (value is null)
        {
            return null;
        }

        value = value.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (value.Length > MaxExternalEnvLength)
        {
            Log.Warning("Ignoring the value of {Key} because it is too long to be sent in an HTTP header", ConfigurationKeys.ExternalEnv);
            return null;
        }

        foreach (var c in value)
        {
            // Only allow printable ASCII characters (including space) in the header value
            if (c < 0x20 || c > 0x7E)
            {
                Log.Warning("Ignoring the value of {Key} because it contains characters that are not allowed in an HTTP header", ConfigurationKeys.ExternalEnv);
                return null;
            }
        }

        return value;
    }
}
