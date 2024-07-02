// <copyright file="TelemetryMetricsFileParserHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public static class TelemetryMetricsFileParserHelpers
    {
        public static bool HasSentProfile(this TelemetryMetricsFileParser parser)
        {
            bool hasSentProfile = false;
            string error = string.Empty;
            string[] containedTags = new[] { "has_sent_profiles:true" };
            hasSentProfile |= parser.Profiles.Any(tm => tm.ContainsTags(containedTags, false, ref error));
            hasSentProfile |= parser.RuntimeIds.Any(tm => tm.ContainsTags(containedTags, false, ref error));

            return hasSentProfile;
        }

        public static void ShouldContainTags(this TelemetryMetricsFileParser parser, string[] containedTags, bool mandatory = false)
        {
            string error = string.Empty;
            foreach (var metric in parser.Profiles)
            {
                Assert.True(metric.ContainsTags(containedTags, mandatory, ref error), $"{error}");
            }

            foreach (var metric in parser.RuntimeIds)
            {
                Assert.True(metric.ContainsTags(containedTags, mandatory, ref error), $"{error}");
            }
        }

        public static void ShouldContainTags(this List<TelemetryMetric> metrics, string[] containedTags, bool mandatory = false)
        {
            string error = string.Empty;
            foreach (var metric in metrics)
            {
                Assert.True(metric.ContainsTags(containedTags, mandatory, ref error), $"{error}");
            }
        }

        public static void AssertTagsContains(this TelemetryMetric metric, string name, string value)
        {
            Assert.Contains(metric.Tags, t => t.Item1 == name && t.Item2 == value);
        }

        public static bool Contains(this List<Tuple<string, string>> tags, string name, string value)
        {
            return tags.Any(t => t.Item1 == name && t.Item2 == value);
        }

        public static bool ContainsTags(this TelemetryMetric metric, string[] containedTags, bool mandatory, ref string error)
        {
            if (metric.Tags == null)
            {
                error = "tags is null";
                return false;
            }

            Dictionary<string, string> containedRefs = new Dictionary<string, string>();
            foreach (var tag in containedTags)
            {
                var kv = tag.Split(':');
                var key = kv[0];
                var value = kv[1];
                containedRefs[key] = value;
            }

            foreach (var tag in metric.Tags)
            {
                var key = tag.Item1;
                var value = tag.Item2;

                if (!containedRefs.TryGetValue(key, out var containedValue))
                {
                    if (mandatory)
                    {
                        error = $"Key {key} not found in containedTags";
                        return false;
                    }
                }
                else
                {
                    if (!value.Contains(containedValue))
                    {
                        error = $"{key}: '{value}' does not contain '{containedValue}'";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
