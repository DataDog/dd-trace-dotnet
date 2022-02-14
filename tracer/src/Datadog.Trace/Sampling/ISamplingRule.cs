// <copyright file="ISamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Sampling
{
    internal interface ISamplingRule
    {
        /// <summary>
        /// Gets the rule name.
        /// Used for debugging purposes mostly.
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// Gets the priority.
        /// Higher number means higher priority.
        /// </summary>
        int Priority { get; }

        int SamplingMechanism { get; }

        bool IsMatch(Span span);

        float GetSamplingRate(Span span);
    }
}
