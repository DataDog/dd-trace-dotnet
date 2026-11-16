// <copyright file="ProfileOutput.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Profiler.IntegrationTests.Helpers;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal sealed class ProfileOutput
    {
        internal ProfileOutput(IReadOnlyList<PprofProfile> profiles)
        {
            if (profiles is null)
            {
                throw new ArgumentNullException(nameof(profiles));
            }

            var copy = new PprofProfile[profiles.Count];
            var samples = new List<ProfileSample>();
            for (var i = 0; i < profiles.Count; i++)
            {
                copy[i] = profiles[i];
                foreach (var sample in profiles[i].Samples)
                {
                    samples.Add(sample);
                }
            }

            Profiles = Array.AsReadOnly(copy);
            Samples = new ProfileSampleSet(samples);
        }

        public IReadOnlyList<PprofProfile> Profiles { get; }

        public ProfileSampleSet Samples { get; }

        public IReadOnlyList<ProfileLabels> Labels => Samples.Labels;

        public IReadOnlyList<StackTrace> CallStacks => Samples.CallStacks;

        public ProfileSampleSet WithNonZeroValue(ProfileValue value) => Samples.WithNonZeroValue(value);

        public ProfileSampleSet WithLabel(string name, string value) => Samples.WithLabel(name, value);

        public long Sum(ProfileValue value) => Samples.Sum(value);
    }
}
