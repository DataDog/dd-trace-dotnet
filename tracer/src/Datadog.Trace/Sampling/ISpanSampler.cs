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
        /// Gets a value indicating whether the sampler contains a sampling rule that depends on span resource names.
        /// </summary>
        /// <remarks>Can be used to perform optimizations, such as delaying the creation of a resource name for spans
        /// which are subsequently going to be sampled, or for which a resource name can't be accurately calculated</remarks>
        /// <returns><c>true</c> if one of the registered rules depends on span resource names, <c>false</c> otherwise</returns>
        bool HasResourceBasedSamplingRule { get; }

        /// <summary>
        ///     Makes a sampling decision on the given <paramref name="span"/> and adds necessary tags to the span.
        /// </summary>
        /// <param name="span">The <see cref="Span"/> to make the sampling decision on and tag if necessary.</param>
        /// <returns><see langword="true"/> when the <paramref name="span"/> is sampled; otherwise, <see langword="false"/>.</returns>
        bool MakeSamplingDecision(Span span);
    }
}
