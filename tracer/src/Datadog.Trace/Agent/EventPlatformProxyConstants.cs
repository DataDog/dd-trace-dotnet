// <copyright file="EventPlatformProxyConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent;

internal static class EventPlatformProxyConstants
{
    internal const string SubdomainHeaderName = "X-Datadog-EVP-Subdomain";
    internal const string EventPlatformIntakeSubdomain = "event-platform-intake";

    /// <summary>
    /// EVP uncompressed request-body limit. Batch splitting happens before transport compression
    /// and includes the request wrapper bytes.
    /// </summary>
    internal const int PayloadSizeLimitBytes = 5 * 1024 * 1024;
}
