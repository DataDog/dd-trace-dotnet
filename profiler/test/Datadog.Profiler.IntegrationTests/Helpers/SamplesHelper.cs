// <copyright file="SamplesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using Perftools.Profiles;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public static class SamplesHelper
    {
        public static int GetSamplesCount(string directory)
        {
            int count = 0;
            foreach (var profile in GetProfiles(directory))
            {
                count += profile.Sample.Count;
            }

            return count;
        }

        public static IEnumerable<Profile> GetProfiles(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.pprof", SearchOption.AllDirectories))
            {
                using var stream = File.OpenRead(file);
                var profile = Profile.Parser.ParseFrom(stream);

                yield return profile;
            }
        }

        public static void CheckSamplesValueCount(string directory, int valuesCount)
        {
            Assert.True(HaveSamplesValueCount(directory, valuesCount));
        }

        private static bool HaveSamplesValueCount(string directory, int valuesCount)
        {
            foreach (var profile in GetProfiles(directory))
            {
                if (profile.SampleType.Count != valuesCount)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
