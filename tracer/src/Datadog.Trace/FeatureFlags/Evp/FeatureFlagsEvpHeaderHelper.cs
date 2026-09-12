// <copyright file="FeatureFlagsEvpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.FeatureFlags.Evp;

/// <summary>
/// Headers used when a Feature Flags event is delivered through a local EVP relay.
/// </summary>
internal sealed class FeatureFlagsEvpHeaderHelper : HttpHeaderHelperBase
{
    internal const string EvpSubdomainHeader = "X-Datadog-EVP-Subdomain";
    internal const string EvpSubdomain = "event-platform-intake";
    internal const string EvpOriginHeader = "DD-EVP-ORIGIN";
    internal const string EvpOrigin = "dd-trace-dotnet";
    internal const string EvpOriginVersionHeader = "DD-EVP-ORIGIN-VERSION";

    public static readonly FeatureFlagsEvpHeaderHelper Instance = new();

    private FeatureFlagsEvpHeaderHelper()
    {
        DefaultHeaders =
        [
            .. AgentHttpHeaderNames.MinimalHeaders,
            new(EvpSubdomainHeader, EvpSubdomain),
            new(EvpOriginHeader, EvpOrigin),
            new(EvpOriginVersionHeader, TracerConstants.ThreePartVersion),
        ];
        HttpSerializedDefaultHeaders =
            $"{AgentHttpHeaderNames.HttpSerializedMinimalHeaders}" +
            $"{EvpSubdomainHeader}: {EvpSubdomain}{DatadogHttpValues.CrLf}" +
            $"{EvpOriginHeader}: {EvpOrigin}{DatadogHttpValues.CrLf}" +
            $"{EvpOriginVersionHeader}: {TracerConstants.ThreePartVersion}{DatadogHttpValues.CrLf}";
    }

    public override KeyValuePair<string, string>[] DefaultHeaders { get; }

    protected override string HttpSerializedDefaultHeaders { get; }
}
