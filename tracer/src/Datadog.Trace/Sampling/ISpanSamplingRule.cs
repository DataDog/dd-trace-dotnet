// <copyright file="ISpanSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Sampling
{
    /// <summary>
    ///     Defines the contract for a sampling rule for single span ingestion.
    /// </summary>
    internal interface ISpanSamplingRule
    {
        /// <summary>
        ///     Gets the probability of keeping a span for this rule.
        /// </summary>
        /// <value><see langword="float"/> in range <c>[0.0, 1.0]</c>.
        /// <para><c>0.0</c> is drop all.</para>
        /// <para><c>1.0</c> is accept all.</para>
        /// </value>
        float SamplingRate { get; }

        /// <summary>
        ///     Gets the maximum number of allowed spans per second for this rule.
        /// </summary>
        /// <value>
        /// A nullable <see langword="float"/>.
        /// <para><see langword="null"/> is the default and indicates unlimited.</para>
        /// </value>
        float? MaxPerSecond { get; }

        /// <summary>
        ///     Determines whether or not the <paramref name="span"/> should be kept or dropped based on this rule.
        /// </summary>
        /// <param name="span">The <see cref="Span"/> to check.</param>
        /// <returns><see langword="true"/> when the span should be kept; otherwise, <see langword="false"/>.</returns>
        bool ShouldKeep(Span span);
    }
}
