// <copyright file="PprofHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Perftools.Profiles;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class PprofHelper
    {
        public static IEnumerable<IList<Label>> Labels(this Profile profile)
        {
            foreach (var sample in profile.Sample)
            {
                yield return sample.Labels(profile).ToList();
            }
        }

        public static IEnumerable<Label> Labels(this Perftools.Profiles.Sample sample, Profile profile)
        {
            return sample.Label.Select(
                label => new Label { Name = profile.StringTable[(int)label.Key], Value = profile.StringTable[(int)label.Str] });
        }

        public static StackTrace StackTrace(this Perftools.Profiles.Sample sample, Profile profile)
        {
            return new StackTrace(
                sample.LocationId
                    .Select(id => profile.Location.First(l => l.Id == id))
                    .Select(l => l.Line[0].FunctionId)
                    .Select(l => profile.Function.First(f => f.Id == l))
                    .Select(f => profile.StringTable[(int)f.Name])
                    .Select(s => new StackFrame(s)));
        }

        internal struct Label
        {
            public string Name;
            public string Value;
        }
    }
}
