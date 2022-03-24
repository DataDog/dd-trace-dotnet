// <copyright file="PprofHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Perftools.Profiles.Tests;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal static class PprofHelper
    {
        public static IEnumerable<Label> Labels(this Profile profile)
        {
            var stringTable = profile.StringTable;

            foreach (var sample in profile.Sample)
            {
                foreach (var label in sample.Label)
                {
                    yield return new Label { Name = stringTable[(int)label.Key], Value = stringTable[(int)label.Str] };
                }
            }
        }

        internal struct Label
        {
            public string Name;
            public string Value;
        }
    }
}
