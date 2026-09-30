// <copyright file="FlagEvaluationAgentHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class FlagEvaluationAgentHeaderHelper : HttpHeaderHelperBase
{
    internal static readonly FlagEvaluationAgentHeaderHelper Instance = new();

    private FlagEvaluationAgentHeaderHelper()
    {
        DefaultHeaders =
        [
            ..AgentHttpHeaderNames.MinimalHeaders,
            new("X-Datadog-EVP-Subdomain", "event-platform-intake"),
            new("DD-EVP-ORIGIN", "dd-trace-dotnet"),
            new("DD-EVP-ORIGIN-VERSION", TracerConstants.ThreePartVersion),
        ];
        HttpSerializedDefaultHeaders = AgentHttpHeaderNames.HttpSerializedMinimalHeaders
                                     + "X-Datadog-EVP-Subdomain: event-platform-intake\r\n"
                                     + "DD-EVP-ORIGIN: dd-trace-dotnet\r\n"
                                     + "DD-EVP-ORIGIN-VERSION: " + TracerConstants.ThreePartVersion + "\r\n";
    }

    public override KeyValuePair<string, string>[] DefaultHeaders { get; }

    protected override string HttpSerializedDefaultHeaders { get; }
}
