// <copyright file="ITraceSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Sampling
{
    internal interface ITraceSampler
    {
        /// <summary>
        /// Gets a value indicating whether the sampler contains a sampling rule that depends on span resource names.
        /// </summary>
        /// <remarks>Can be used to perform optimizations, such as delaying the creation of a resource name for spans
        /// which are subsequently going to be sampled, or for which a resource name can't be accurately calculated</remarks>
        /// <returns><c>true</c> if one of the registered rules depends on span resource names, <c>false</c> otherwise</returns>
        bool HasResourceBasedSamplingRule { get; }

        void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates);

        SamplingDecision MakeSamplingDecision(in SamplingContext context);
    }
}
