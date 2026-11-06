// <copyright file="ProfileSampleSet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Profiler.IntegrationTests.Helpers;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal sealed class ProfileSampleSet : IReadOnlyList<ProfileSample>
    {
        private readonly ProfileSample[] _samples;

        internal ProfileSampleSet(IEnumerable<ProfileSample> samples)
        {
            if (samples is null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            var copy = new List<ProfileSample>();
            foreach (var sample in samples)
            {
                copy.Add(sample);
            }

            _samples = copy.ToArray();
        }

        public int Count => _samples.Length;

        public IReadOnlyList<ProfileLabels> Labels
        {
            get
            {
                var labels = new ProfileLabels[_samples.Length];
                for (var i = 0; i < _samples.Length; i++)
                {
                    labels[i] = _samples[i].Labels;
                }

                return Array.AsReadOnly(labels);
            }
        }

        public IReadOnlyList<StackTrace> CallStacks
        {
            get
            {
                var stacks = new StackTrace[_samples.Length];
                for (var i = 0; i < _samples.Length; i++)
                {
                    stacks[i] = _samples[i].CallStack;
                }

                return Array.AsReadOnly(stacks);
            }
        }

        public ProfileSample this[int index] => _samples[index];

        public ProfileSampleSet WithNonZeroValue(ProfileValue value)
        {
            var matches = new List<ProfileSample>();
            foreach (var sample in _samples)
            {
                var read = sample.Values.TryGetValue(value);
                if (read.IsPresent && read.Value != 0)
                {
                    matches.Add(sample);
                }
            }

            return new ProfileSampleSet(matches);
        }

        public ProfileSampleSet WithLabel(string name, string value)
        {
            var matches = new List<ProfileSample>();
            foreach (var sample in _samples)
            {
                foreach (var candidate in sample.Labels.GetValues(name))
                {
                    if (string.Equals(candidate, value, StringComparison.Ordinal))
                    {
                        matches.Add(sample);
                        break;
                    }
                }
            }

            return new ProfileSampleSet(matches);
        }

        public long Sum(ProfileValue value)
        {
            long sum = 0;
            foreach (var sample in _samples)
            {
                sum += sample.Values.GetRequired(value);
            }

            return sum;
        }

        public IEnumerator<ProfileSample> GetEnumerator() => ((IEnumerable<ProfileSample>)_samples).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
