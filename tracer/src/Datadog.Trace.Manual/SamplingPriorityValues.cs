// <copyright file="SamplingPriorityValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Datadog.Trace
{
    internal static class SamplingPriorityValues
    {
        /// <summary>
        /// The default sampling priority used when there is no sampler available
        /// or no sampling rules match. For example, this value is used if the tracer has not yet
        /// received any sampling rates from agent and there are no configured sampling rates.
        /// </summary>
        public const int Default = AutoKeep;

        /// <summary>
        /// Trace should be dropped (not sampled).
        /// Sampling decision made explicitly by user through
        /// code or configuration (e.g. the rules sampler).
        /// </summary>
        public const int UserReject = -1;

        /// <summary>
        /// Trace should be dropped (not sampled).
        /// Sampling decision made by the built-in sampler.
        /// </summary>
        public const int AutoReject = 0;

        /// <summary>
        /// Trace should be kept (sampled).
        /// Sampling decision made by the built-in sampler.
        /// </summary>
        public const int AutoKeep = 1;

        /// <summary>
        /// Trace should be kept (sampled).
        /// Sampling decision made explicitly by user through
        /// code or configuration (e.g. the rules sampler).
        /// </summary>
        public const int UserKeep = 2;

        [return: NotNullIfNotNull("samplingPriority")]
        internal static string? ToString(int? samplingPriority)
        {
            return samplingPriority switch
            {
                2 => "2",
                1 => "1",
                0 => "0",
                -1 => "-1",
                null => null,
                { } value => value.ToString(CultureInfo.InvariantCulture)
            };
        }

        internal static bool IsKeep(int samplingPriority) => samplingPriority > 0;

        internal static bool IsDrop(int samplingPriority) => samplingPriority <= 0;
    }
}
