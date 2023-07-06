// <copyright file="SamplesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public static long GetValueSum(string directory, int valueIndex)
        {
            long sum = 0;
            foreach (var profile in GetProfiles(directory))
            {
                foreach (var sample in profile.Sample)
                {
                    var val = sample.Value[valueIndex];
                    sum += val;
                }
            }

            return sum;
        }

        public static int GetThreadCount(string directory)
        {
            List<string> threadNames = new List<string>(16);
            foreach (var profile in GetProfiles(directory))
            {
                foreach (var sample in profile.Sample)
                {
                    foreach (var label in sample.Labels(profile))
                    {
                        if (label.Name == "thread id")
                        {
                            if (!threadNames.Contains(label.Value))
                            {
                                threadNames.Add(label.Value);
                            }

                            continue;
                        }
                    }
                }
            }

            return threadNames.Count;
        }

        public static bool IsLabelPresent(string directory, string labelName)
        {
            foreach (var profile in GetProfiles(directory))
            {
                foreach (var sample in profile.Sample)
                {
                    bool labelIsHere = false;
                    foreach (var label in sample.Labels(profile))
                    {
                        if (label.Name == labelName)
                        {
                            labelIsHere = true;
                        }
                    }

                    if (!labelIsHere)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static IEnumerable<(StackTrace StackTrace, Label[] Labels, long[] Values)> GetSamples(string directory)
        {
            foreach (var profile in GetProfiles(directory))
            {
                foreach (var sample in profile.Sample)
                {
                    yield return (sample.StackTrace(profile), sample.Label.ToArray(), sample.Value.ToArray());
                }
            }
        }

        internal static IEnumerable<(string Type, string Message, long Count, StackTrace Stacktrace)> ExtractExceptionSamples(string directory)
        {
            static IEnumerable<(string Type, string Message, long Count, StackTrace Stacktrace, long Time)> SamplesWithTimestamp(string directory)
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.pprof", SearchOption.AllDirectories))
                {
                    using var stream = File.OpenRead(file);

                    var profile = Profile.Parser.ParseFrom(stream);

                    foreach (var sample in profile.Sample)
                    {
                        var count = sample.Value[0];

                        if (count == 0)
                        {
                            continue;
                        }

                        var labels = sample.Labels(profile).ToArray();

                        var type = labels.Single(l => l.Name == "exception type").Value;
                        var message = labels.Single(l => l.Name == "exception message").Value;

                        yield return (type, message, count, sample.StackTrace(profile), profile.TimeNanos);
                    }
                }
            }

            return SamplesWithTimestamp(directory)
                .OrderBy(s => s.Time)
                .Select(s => (s.Type, s.Message, s.Count, s.Stacktrace));
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
