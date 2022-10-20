// <copyright file="ISpanSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling
{
    /// <summary>
    ///     Represents the functionality to sample individual spans instead of entire traces.
    /// </summary>
    internal interface ISpanSampler
    {
        /// <summary>
        ///     Makes a sampling decision on the given <paramref name="span"/> and adds necessary tags to the span.
        /// </summary>
        /// <param name="span">The <see cref="Span"/> to make the sampling decision on and tag if necessary.</param>
        /// <returns><see langword="true"/> when the <paramref name="span"/> is sampled; otherwise, <see langword="false"/>.</returns>
        bool MakeSamplingDecision(Span span);
    }
}
