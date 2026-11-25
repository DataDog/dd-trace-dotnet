// <copyright file="ProfilerSamples.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    /// <summary>
    /// Profiler-specific views of the samples: exception and allocation rows, in profile time order.
    /// </summary>
    internal static class ProfilerSamples
    {
        public static string ExceptionType(this ProfileSample sample) => sample.Labels.GetRequired("exception type");

        public static string ExceptionMessage(this ProfileSample sample) => sample.Labels.GetRequired("exception message");

        public static string AllocationClass(this ProfileSample sample) => sample.Labels.GetRequired("allocation class");

        /// <summary>
        /// Samples with a non-zero <paramref name="value"/>, ordered by <see cref="PprofProfile.TimeNanos"/>.
        /// Samples of one profile keep their order.
        /// </summary>
        public static IReadOnlyList<ProfileSample> InProfileTimeOrder(this ProfileOutput output, ProfileValue value)
        {
            return output.Profiles
                .OrderBy(profile => profile.TimeNanos)
                .SelectMany(profile => new ProfileSampleSet(profile.Samples).WithNonZeroValue(value))
                .ToList();
        }

        public static IReadOnlyList<(string Type, string Message, long Count, StackTrace Stacktrace)> ExceptionSamples(this ProfileOutput output)
        {
            return output.InProfileTimeOrder(ProfileValue.ExceptionCount)
                .Select(sample => (sample.ExceptionType(), sample.ExceptionMessage(), sample.Values.GetRequired(ProfileValue.ExceptionCount), sample.CallStack))
                .ToList();
        }

        /// <summary>
        /// Size is 0 when the sample has no allocation size (.NET Framework).
        /// </summary>
        public static IReadOnlyList<(string Type, long Count, long Size, StackTrace Stacktrace)> AllocationSamples(this ProfileOutput output)
        {
            return output.InProfileTimeOrder(ProfileValue.AllocationCount)
                .Select(sample =>
                {
                    var size = sample.Values.TryGetValue(ProfileValue.AllocationSize);
                    return (sample.AllocationClass(), sample.Values.GetRequired(ProfileValue.AllocationCount), size.IsPresent ? size.Value : 0, sample.CallStack);
                })
                .ToList();
        }
    }
}
