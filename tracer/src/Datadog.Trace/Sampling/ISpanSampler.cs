// <copyright file="ISpanSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Sampling
{
    /// <summary>
    ///     Represents the functionality to sample individual spans instead of entire traces.
    /// </summary>
    internal interface ISpanSampler
    {
        /// <summary>
        ///     Makes a <see cref="SamplingDecision"/> for the given <paramref name="span"/>.
        /// </summary>
        /// <param name="span">The <see cref="Span"/> to sample.</param>
        void MakeSamplingDecision(Span span);

        /// <summary>
        ///     Tags the <paramref name="span"/> with the necessary tags for single span ingestion.
        /// </summary>
        /// <param name="span">The <see cref="Span"/> to tag.</param>
        /// <param name="rule">The <see cref="ISpanSamplingRule"/> that contains the tag information.</param>
        void Tag(Span span, ISpanSamplingRule rule);
    }
}
