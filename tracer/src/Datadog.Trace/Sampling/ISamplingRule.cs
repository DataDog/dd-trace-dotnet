// <copyright file="ISamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling
{
    internal interface ISamplingRule
    {
        string SamplingMechanism { get; }

        bool IsResourceBasedSamplingRule { get; }

        bool IsMatch(in SamplingContext span);

        float GetSamplingRate(in SamplingContext span);
    }
}
