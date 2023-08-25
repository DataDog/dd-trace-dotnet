// <copyright file="MetricTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Metrics;

public class MetricTests
{
    private static readonly Dictionary<string, List<string>> IgnoredTagsByMetricName = new()
    {
        { "waf.init", new() { "event_rules_version" } }, // we don't send this tag as cardinality is infinite
        { "waf.updates", new() { "event_rules_version" } }, // we don't send this tag as cardinality is infinite
        { "waf.requests", new() { "event_rules_version" } }, // we don't send this tag as cardinality is infinite
        { "spans_finished", new() { "integration_name" } }, // this is technically difficult for us, so we don't tag it
    };

    private static readonly Dictionary<string, List<string>> OneOfTagsByMetricName = new()
    {
        { "init_time", new() { "total", "component" } }, // we only send one of these
    };

    [Fact]
    public void OnlyAllowedMetricsAreSubmitted()
    {
        // Only metrics defined in the following json documents should be submitted
        // https://github.com/DataDog/dd-go/trace/apps/tracer-telemetry-intake/telemetry-metrics/static/common_metrics.json
        // https://github.com/DataDog/dd-go/trace/apps/tracer-telemetry-intake/telemetry-metrics/static/dotnet_metrics.json
        //
        // These are duplicated in this repo. When adding new metrics, add them into the embedded json files here, then
        // after merging, update the source JSON file in dd-go

        var expected = GetIntakeMetricsAndTags();
        var actual = GetImplementedMetricsAndTags();

        foreach (var implementation in actual)
        {
            var expectedMetric = expected.FirstOrDefault(
                x => string.Equals(x.Namespace, implementation.Namespace)
                  && string.Equals(x.Metric, implementation.Metric))!;

            expectedMetric.Should().NotBeNull($"Metric {implementation.Metric} with namespace {implementation.Namespace} was not found in intake metrics");

            // compare everything except tags
            implementation.Should().BeEquivalentTo(expectedMetric, options => options.ExcludingMissingMembers());

            // check that we have the same number of tags as expected
            // this isn't correct for some tags, but it's true of most,
            // so we assert it and have an exception list where it's not required
            var expectedPrefixes = IgnoredTagsByMetricName.TryGetValue(expectedMetric.Metric, out var ignored)
                                       ? expectedMetric.TagPrefixes.Except(ignored).ToList()
                                       : expectedMetric.TagPrefixes;

            // if we have any expected prefixes, we should expect some permutations
            if (expectedPrefixes.Count > 0)
            {
                implementation.TagPermutations.Should().NotBeEmpty($"{implementation.Metric} should only use expected prefixes ({string.Join(",", expectedMetric.TagPrefixes)})");
            }

            // Check all our permutation are valid
            foreach (var permutation in implementation.TagPermutations)
            {
                var permutationPrefixes = permutation.Select(
                    tag => tag.IndexOf(':') is var i and >= 0
                               ? tag.Substring(0, i)
                               : tag);

                permutationPrefixes.Should()
                                   .OnlyContain(prefix => expectedPrefixes.Contains(prefix), $"{implementation.Metric} should only use expected prefixes ({string.Join(",", expectedMetric.TagPrefixes)})")
                                   .And.OnlyHaveUniqueItems();

                // for "one of" cases we only expect to send one of the specified tags, so reduce the expected count
                var expectedCount = OneOfTagsByMetricName.TryGetValue(expectedMetric.Metric, out var oneOfList)
                                        ? expectedPrefixes.Count - (oneOfList.Count - 1)
                                        : expectedPrefixes.Count;

                permutation.Should().HaveCount(expectedCount, $"{implementation.Metric} should supply all the expected tags ({string.Join(",", expectedMetric.TagPrefixes)})");
            }
        }
    }

    private static List<ImplementedMetricAndTags> GetImplementedMetricsAndTags()
    {
        var results = new List<ImplementedMetricAndTags>();

        results.AddRange(GetCounts());
        var counts = results.Count;
        results.AddRange(GetGauges());
        var gauges = results.Count - counts;
        results.AddRange(GetDistributions());
        var distributions = results.Count - gauges;

        results.Should().NotBeEmpty();
        counts.Should().NotBe(0);
        gauges.Should().NotBe(0);
        distributions.Should().NotBe(0);
        return results;

        static IEnumerable<ImplementedMetricAndTags> GetCounts()
        {
            var metricType = typeof(Count);
            var allMetrics = Enum.GetValues(metricType);
            foreach (Count metric in allMetrics)
            {
                var metricName = metric.GetName();
                var isCommon = metric.IsCommon();
                var metricNamespace = metric.GetNamespace();
                if (isCommon && metricNamespace is null)
                {
                    metricNamespace = MetricNamespaceConstants.Tracer;
                }

                var member = metricType.GetField(metric.ToString());
                var tags = GetTagPermutations(member, metricNamespace);
                yield return new ImplementedMetricAndTags(metricNamespace, metricName, isCommon, TelemetryMetricType.Count, tags);
            }
        }

        static IEnumerable<ImplementedMetricAndTags> GetGauges()
        {
            var metricType = typeof(Gauge);
            var allMetrics = Enum.GetValues(metricType);
            foreach (Gauge metric in allMetrics)
            {
                var metricName = metric.GetName();
                var isCommon = metric.IsCommon();
                var metricNamespace = metric.GetNamespace();
                if (isCommon && metricNamespace is null)
                {
                    metricNamespace = MetricNamespaceConstants.Tracer;
                }

                var member = metricType.GetField(metric.ToString());
                var tags = GetTagPermutations(member, metricNamespace);
                yield return new ImplementedMetricAndTags(metricNamespace, metricName, isCommon, TelemetryMetricType.Gauge, tags);
            }
        }

        static IEnumerable<ImplementedMetricAndTags> GetDistributions()
        {
            var metricType = typeof(Distribution);
            var allMetrics = Enum.GetValues(metricType);
            foreach (Distribution metric in allMetrics)
            {
                var metricName = metric.GetName();
                var isCommon = metric.IsCommon();
                var metricNamespace = metric.GetNamespace();
                if (isCommon && metricNamespace is null)
                {
                    metricNamespace = MetricNamespaceConstants.Tracer;
                }

                var member = metricType.GetField(metric.ToString());
                var tags = GetTagPermutations(member, metricNamespace);
                yield return new ImplementedMetricAndTags(metricNamespace, metricName, isCommon, TelemetryMetricType.Distribution, tags);
            }
        }

        static List<string[]> GetTagPermutations(FieldInfo member, string ns)
        {
            // Can't grab open generic attributes using GetCustomAttributes(typeof()) directly it seems
            foreach (var attr in member.GetCustomAttributesData())
            {
                var attributeType = attr.AttributeType;
                if (attributeType == typeof(TelemetryMetricAttribute))
                {
                    // no tags unless this is an ASM metric, in which case we include waf_version
                    // see src\Datadog.Trace\Telemetry\Collectors\MetricsTelemetryCollector.GetTags(string? ns, string[]? metricKeyTags)
                    if (ns == MetricNamespaceConstants.ASM)
                    {
                        return new() { new[] { "waf_version:unknown" } };
                    }

                    return new List<string[]>();
                }

                if (attributeType.IsGenericType)
                {
                    var genericDefinition = attributeType.GetGenericTypeDefinition();
                    if (genericDefinition == typeof(TelemetryMetricAttribute<>))
                    {
                        // one tag, grab the tags
                        return GetAllTagPermutations(attributeType.GenericTypeArguments[0]).ToList();
                    }

                    if (genericDefinition == typeof(TelemetryMetricAttribute<,>))
                    {
                        // two tags, grab the tags
                        var tags1 = GetAllTagPermutations(attributeType.GenericTypeArguments[0]);
                        var tags2 = GetAllTagPermutations(attributeType.GenericTypeArguments[1]);
                        return (from tagset1 in tags1
                                from tagset2 in tags2
                                select tagset1.Concat(tagset2).ToArray()).ToList();
                    }
                }
            }

            throw new InvalidOperationException($"Error getting tag prefixes for {member.DeclaringType.Name}.{member.Name}: No {nameof(TelemetryMetricAttribute)} declared");

            static IEnumerable<string[]> GetAllTagPermutations(Type tagType)
            {
                var tagValues = Enum.GetValues(tagType);
                foreach (var tagValue in tagValues)
                {
                    var member = tagType.GetField(tagValue.ToString());
                    var descriptionAttribute = member.GetCustomAttribute<DescriptionAttribute>();
                    var description = descriptionAttribute.Description;

                    // For ASM we use a trick here to handle multiple tags that are only used in a limited subset of combinations
                    yield return description.Split(';');
                }
            }
        }
    }

    private static List<AllowedMetricAndTagPrefixes> GetIntakeMetricsAndTags()
    {
        var jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy(), }
        };

        var rawMetrics = GetMetricsData("common_metrics.json");
        var commonMetrics = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, IntakeMetric>>>(rawMetrics, jsonSettings);

        rawMetrics = GetMetricsData("dotnet_metrics.json");
        // Note that dotnet doesn't have a namespace
        var dotnetMetrics = JsonConvert.DeserializeObject<Dictionary<string, IntakeMetric>>(rawMetrics, jsonSettings);

        var results = new List<AllowedMetricAndTagPrefixes>(commonMetrics.Count + dotnetMetrics.Count);

        foreach (var commonMetricNamespace in commonMetrics)
        {
            var metricNamespace = commonMetricNamespace.Key;
            foreach (var metricDetails in commonMetricNamespace.Value)
            {
                var details = metricDetails.Value;
                results.Add(new AllowedMetricAndTagPrefixes(metricNamespace, metricDetails.Key, IsCommon: true, details.MetricType, details.Tags));
            }
        }

        foreach (var metricDetails in dotnetMetrics)
        {
            var metricName = metricDetails.Key;
            var details = metricDetails.Value;
            results.Add(new AllowedMetricAndTagPrefixes(Namespace: null, metricName, IsCommon: false, details.MetricType, details.Tags));
        }

        results.Should().NotBeEmpty();
        return results;

        static string GetMetricsData(string filename)
        {
            var thisAssembly = typeof(MetricTests).Assembly;
            var stream = thisAssembly.GetManifestResourceStream($"Datadog.Trace.Tests.Telemetry.Metrics.{filename}");
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
    }

    public record AllowedMetricAndTagPrefixes(string Namespace, string Metric, bool IsCommon, string MetricType, List<string> TagPrefixes);

    public record ImplementedMetricAndTags(string Namespace, string Metric, bool IsCommon, string MetricType, List<string[]> TagPermutations);

    public class IntakeMetric
    {
        public List<string> Tags { get; set; }

        public string MetricType { get; set; }
    }
}
