// <copyright file="SamplingRuleProvenance.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling;

/// <summary>
/// The provenance of a sampling rule.
/// </summary>
internal static class SamplingRuleProvenance
{
    /// <summary>
    /// Sampling rule is from local configuration,
    /// such as <c>DD_TRACE_SAMPLING_RULES</c> or <c>DD_TRACE_SAMPLE_RATE</c>.
    /// </summary>
    public const string Local = "local";

    /// <summary>
    /// Sampling rule is a manual user override
    /// sent via remote configuration (RCM).
    /// </summary>
    public const string RemoteCustomer = "customer";

    /// <summary>
    /// Sampling rule was automatically computed by Datadog
    /// and sent via remote configuration (RCM).
    /// </summary>
    public const string RemoteDynamic = "dynamic";
}
