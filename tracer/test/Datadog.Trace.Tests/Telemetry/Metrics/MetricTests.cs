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
    private static readonly Dictionary<string, string[]> IgnoredTagsByMetricName = new()
    {
        { "waf.init", new[] { "event_rules_version" } }, // we don't send this tag as cardinality is infinite
        { "waf.updates", new[] { "event_rules_version" } }, // we don't send this tag as cardinality is infinite
        { "waf.requests", new[] { "event_rules_version" } }, // we don't send this tag as cardinality is infinite
        { "spans_finished", new[] { "integration_name" } }, // this is technically difficult for us, so we don't tag it
        { "trace_chunks_dropped", ["src_library"] }, // this is optional, only added by the rust library
        { "trace_chunks_sent", ["src_library"] }, // this is optional, only added by the rust library
        { "trace_api.requests", ["src_library"] }, // this is optional, only added by the rust library
        { "trace_api.bytes", ["src_library"] }, // this is optional, only added by the rust library
        { "trace_api.responses", ["src_library"] }, // this is optional, only added by the rust library
        { "trace_api.errors", ["src_library"] }, // this is optional, only added by the rust library
    };

    private static readonly Dictionary<string, string[]> OptionalTagsByMetricName = new()
    {
        { "event_created", new[] { "has_codeowner", "is_unsupported_ci", "is_benchmark" } },
        { "event_finished", new[] { "has_codeowner", "is_unsupported_ci", "is_benchmark", "is_new", "early_flake_detection_abort_reason", "browser_driver", "is_rum" } },
        { "git_requests.settings_response", new[] { "coverage_enabled", "itrskip_enabled", "early_flake_detection_enabled" } },
        { "endpoint_payload.requests_errors", ["status_code"] },
        { "git_requests.search_commits_errors", ["status_code"] },
        { "git_requests.objects_pack_errors", ["status_code"] },
        { "git_requests.settings_errors", ["status_code"] },
        { "itr_skippable_tests.request_errors", ["status_code"] },
        { "early_flake_detection.request_errors", ["status_code"] },
        { "endpoint_payload.requests", ["rq_compressed"] },
        { "git_requests.search_commits", ["rq_compressed"] },
        { "git_requests.objects_pack", ["rq_compressed"] },
        { "git_requests.settings", ["rq_compressed"] },
        { "itr_skippable_tests.request", ["rq_compressed"] },
        { "early_flake_detection.request", ["rq_compressed"] },
        { "git_requests.search_commits_ms", ["rs_compressed"] },
        { "itr_skippable_tests.response_bytes", ["rs_compressed"] },
        { "early_flake_detection.response_bytes", ["rs_compressed"] },
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
            var ignoredPrefixes = IgnoredTagsByMetricName.TryGetValue(expectedMetric.Metric, out var ignored) ? ignored : Array.Empty<string>();
            var optionalPrefixes = OptionalTagsByMetricName.TryGetValue(expectedMetric.Metric, out var optional) ? optional : Array.Empty<string>();
            var expectedPrefixes = expectedMetric.TagPrefixes.Except(ignoredPrefixes).Except(optionalPrefixes).ToList();

            // if we have any expected prefixes, we should expect some permutations
            if (expectedPrefixes.Count > 0)
            {
                implementation.TagPermutations.Should().NotBeEmpty($"{implementation.Metric} should only use expected prefixes ({string.Join(",", expectedMetric.TagPrefixes)})");
            }

            // if (IgnoreTagsPermutationValidation.IndexOf(implementation.Metric) != -1)
            // {
            //     continue;
            // }

            // Check all our permutation are valid
            foreach (var permutation in implementation.TagPermutations)
            {
                var permutationPrefixes = permutation
                                         .Select(
                                              tag => tag.IndexOf(':') is var i and >= 0
                                                         ? tag.Substring(0, i)
                                                         : tag)
                                         .Except(optionalPrefixes)
                                         .Where(x => !string.IsNullOrEmpty(x))
                                         .ToList();

                if (permutationPrefixes.Count == 0)
                {
                    permutationPrefixes.Should().HaveCount(expectedPrefixes.Count);
                }
                else
                {
                    permutationPrefixes
                       .Should()
                       .OnlyContain(prefix => expectedPrefixes.Contains(prefix), $"{implementation.Metric} should only use expected prefixes ({string.Join(",", expectedMetric.TagPrefixes)})")
                       .And.OnlyHaveUniqueItems();

                    // for "one of" cases we only expect to send one of the specified tags, so reduce the expected count
                    var expectedCount = OneOfTagsByMetricName.TryGetValue(expectedMetric.Metric, out var oneOfList)
                                            ? expectedPrefixes.Count - (oneOfList.Count - 1)
                                            : expectedPrefixes.Count;

                    var permutationExcludingOptional = permutation.Where(
                        item =>
                        {
                            item = item.Contains(":") ? item.Substring(0, item.IndexOf(":", StringComparison.Ordinal)) : item;
                            return !optionalPrefixes.Contains(item);
                        });
                    permutationExcludingOptional.Should().HaveCount(expectedCount, $"Received: {permutation.Length}, {implementation.Metric} should supply all the expected tags ({string.Join(",", expectedMetric.TagPrefixes)})");
                }
            }
        }
    }

    private static List<ImplementedMetricAndTags> GetImplementedMetricsAndTags()
    {
        var results = new List<ImplementedMetricAndTags>();

        var allMetrics = new[]
        {
            GetValues<Count>(TelemetryMetricType.Count, x => (((Count)x).GetName(), ((Count)x).GetNamespace(), ((Count)x).IsCommon())),
            GetValues<CountShared>(TelemetryMetricType.Count, x => (((CountShared)x).GetName(), ((CountShared)x).GetNamespace(), ((CountShared)x).IsCommon())),
            GetValues<CountCIVisibility>(TelemetryMetricType.Count, x => (((CountCIVisibility)x).GetName(), ((CountCIVisibility)x).GetNamespace(), ((CountCIVisibility)x).IsCommon())),
            GetValues<Gauge>(TelemetryMetricType.Gauge, x => (((Gauge)x).GetName(), ((Gauge)x).GetNamespace(), ((Gauge)x).IsCommon())),
            GetValues<DistributionShared>(TelemetryMetricType.Distribution, x => (((DistributionShared)x).GetName(), ((DistributionShared)x).GetNamespace(), ((DistributionShared)x).IsCommon())),
            GetValues<DistributionCIVisibility>(TelemetryMetricType.Distribution, x => (((DistributionCIVisibility)x).GetName(), ((DistributionCIVisibility)x).GetNamespace(), ((DistributionCIVisibility)x).IsCommon())),
        };

        foreach (var metrics in allMetrics)
        {
            results.AddRange(metrics);
        }

        return results;

        static IEnumerable<ImplementedMetricAndTags> GetValues<T>(
            string type,
            Func<object, (string Name, string NameSpace, bool IsCommon)> getDetails)
            where T : Enum
        {
            var metricType = typeof(T);
            var allMetrics = Enum.GetValues(metricType);
            foreach (var metric in allMetrics)
            {
                var (metricName, metricNamespace, isCommon) = getDetails(metric);
                if (isCommon && metricNamespace is null)
                {
                    metricNamespace = MetricNamespaceConstants.Tracer;
                }

                var member = metricType.GetField(metric.ToString());
                var tags = GetTagPermutations(member, metricNamespace);
                yield return new ImplementedMetricAndTags(metricNamespace, metricName, isCommon, type, tags);
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
