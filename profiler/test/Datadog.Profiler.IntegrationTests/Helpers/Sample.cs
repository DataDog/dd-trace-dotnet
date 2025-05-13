// <copyright file="Sample.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Perftools.Profiles;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal class Sample
    {
        private Sample(Dictionary<string, long> values, Dictionary<string, string> labels)
        {
            Values = values;
            Labels = labels;
        }

        public Dictionary<string, string> Labels { get; }
        public Dictionary<string, long> Values { get; }

        public static Sample Create(Profile profile, Perftools.Profiles.Sample sample)
        {
            var labels = sample.Labels(profile).ToDictionary(k => k.Name, k => k.Value);
            var values = profile.SampleType.Select(s => profile.StringTable[(int)s.Type]).Zip(sample.Value).ToDictionary();
            return new Sample(values, labels);
        }
    }
}
