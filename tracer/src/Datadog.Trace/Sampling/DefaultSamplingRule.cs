// <copyright file="DefaultSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling
{
    // Keeps all traces when no other rule matches. Used instead of AgentSamplingRule
    // when traces are exported over OTLP, because the Agent doesn't send sampling rates.
    internal sealed class DefaultSamplingRule : ISamplingRule
    {
        public string SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.Default;

        public bool IsResourceBasedSamplingRule => false;

        public bool IsMatch(Span span) => true;

        public float GetSamplingRate(Span span) => 1;

        public override string ToString() => "Default";
    }
}
