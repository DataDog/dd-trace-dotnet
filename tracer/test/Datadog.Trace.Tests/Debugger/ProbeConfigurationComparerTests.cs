// <copyright file="ProbeConfigurationComparerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using FluentAssertions;
using Xunit;
using DebuggerTags = Datadog.Trace.Debugger.Configurations.Models.Tags;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeConfigurationComparerTests
{
    [Fact]
    public void WhenDefaultConfiguration_NothingChanged()
    {
        var current = new ProbeConfiguration();
        var incoming = new ProbeConfiguration();

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSamplingNull_IncomingSamplingEmpty_RateLimitChanged()
    {
        var current = new ProbeConfiguration();
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeTrue();
    }

    [Fact]
    public void CurrentSamplingEmpty_IncomingSamplingEmpty_RateLimitNotChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling() } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSamplingHasValue_IncomingSamplingEmpty_RateLimitChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 5 } } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeTrue();
    }

    [Fact]
    public void CurrentSamplingHasValue_IncomingSamplingSameValue_RateLimitNotChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 5 } } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { Sampling = new Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 5 } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentFilterNull_IncomingFilterEmpty_FilterRelatedChanged()
    {
        var current = new ProbeConfiguration();
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentFilterEmpty_IncomingFilterEmpty_FilterRelatedNotChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList() } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentFilterNotEmpty_IncomingFilterEmpty_FilterRelatedChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList() } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList { Classes = Array.Empty<string>() } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentFilterValue_IncomingFilterSameValue_FilterRelatedNotChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList { Classes = new string[] { "1", "2" } } } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList { Classes = new string[] { "1", "2" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentFilterValue_IncomingFilterDifferentValue_FilterRelatedNotChanged()
    {
        var current = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList { Classes = new string[] { "1", "2" } } } };
        var incoming = new ProbeConfiguration { ServiceConfiguration = new ServiceConfiguration() { AllowList = new FilterList { Classes = new string[] { "1", "2", "3" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnaphotsEmpty_IncomingSnapshotsWithDefault_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration {  };
        var incoming = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnaphotsNotEmptyWithDefault_IncomingSnapshotsNotEmptyWithDefault_ProbeRelatedNotChanged()
    {
        var current = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() } };
        var incoming = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnaphotsValue_IncomingSnapshotsBaseValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() } };
        var incoming = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() { Version = 5 } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnaphotsValue_IncomingSnapshotsTypeValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() } };
        var incoming = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() { Capture = new Capture() } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnapshotsCaptureExpressionChanged_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration
        {
            LogProbes = new LogProbe[]
            {
                new()
                {
                    Id = "probe",
                    CaptureExpressions =
                    [
                        new CaptureExpression { Name = "value", Expr = new SnapshotSegment(string.Empty, @"{""ref"":""value""}", null) }
                    ]
                }
            }
        };
        var incoming = new ProbeConfiguration
        {
            LogProbes = new LogProbe[]
            {
                new()
                {
                    Id = "probe",
                    CaptureExpressions =
                    [
                        new CaptureExpression { Name = "value", Expr = new SnapshotSegment(string.Empty, @"{""ref"":""otherValue""}", null) }
                    ]
                }
            }
        };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnaphotsWhereValue_IncomingSnapshotsWhereSameValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "c:/temp/temp.log" } } } };
        var incoming = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "c:/temp/temp.log" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnaphotsWhereValue_IncomingSnapshotsWhereAnotherValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "d:/temp/temp.log" } } } };
        var incoming = new ProbeConfiguration { LogProbes = new LogProbe[] { new LogProbe() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "c:/temp/temp.log" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsEmpty_IncomingMetricsWithDefault_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration {  };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsNotEmptyWithDefault_IncomingMetricsNotEmptyWithDefault_ProbeRelatedNotChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsValue_IncomingMetricsBaseValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() { Version = 5 } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsValue_IncomingMetricsTypeValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() { MetricName = "MetricName" } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsWhereValue_IncomingMetricsWhereSameValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "c:/temp/temp.log" } } } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new MetricProbe() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "c:/temp/temp.log" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsWhereValue_IncomingMetricsWhereAnotherValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new() { Where = new Where() { Lines = new[] { "56" }, SourceFile = "c:/temp/temp.log" } } } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new() { Where = new Where() { Lines = new[] { "57" }, SourceFile = "c:/temp/temp.log" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsValue_IncomingMetricsAnotherValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new() { Value = new SnapshotSegment { Dsl = "Some" } } } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new() { Value = new SnapshotSegment { Dsl = "AweSome" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeTrue();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsValue_IncomingMetricsSameValue_ProbeRelatedChanged()
    {
        var current = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new() { Value = new SnapshotSegment { Dsl = "Some" } } } };
        var incoming = new ProbeConfiguration { MetricProbes = new MetricProbe[] { new() { Value = new SnapshotSegment { Dsl = "Some" } } } };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSnapshotsReparsedJsonContent_ProbeRelatedNotChanged()
    {
        // Reproduces the RCM re-apply scenario: two LogProbe instances that are content-
        // identical but whose SnapshotSegment.Json fields are independently parsed JObject
        // instances (different references, same content) - which is exactly what
        // DynamicInstrumentation.Deserialize<LogProbe> produces on every RCM poll.
        // Without content-based equality on SnapshotSegment, the comparer would treat
        // the probe as added every cycle and trigger unnecessary IL re-instrumentation.
        const string whenJson = @"{""ne"":[{""ref"":""x""},null]}";
        const string refJson = @"{""ref"":""x""}";

        var current = new ProbeConfiguration
        {
            LogProbes = new[]
            {
                new LogProbe
                {
                    Id = "probe-1",
                    When = new SnapshotSegment("x != null", whenJson, string.Empty),
                    Segments = new[]
                    {
                        new SnapshotSegment(string.Empty, null, "x="),
                        new SnapshotSegment("x", refJson, string.Empty),
                    },
                },
            },
        };
        var incoming = new ProbeConfiguration
        {
            LogProbes = new[]
            {
                new LogProbe
                {
                    Id = "probe-1",
                    When = new SnapshotSegment("x != null", whenJson, string.Empty),
                    Segments = new[]
                    {
                        new SnapshotSegment(string.Empty, null, "x="),
                        new SnapshotSegment("x", refJson, string.Empty),
                    },
                },
            },
        };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentMetricsReparsedJsonContent_ProbeRelatedNotChanged()
    {
        const string valueJson = @"{""ref"":""duration""}";

        var current = new ProbeConfiguration
        {
            MetricProbes = new[]
            {
                new MetricProbe
                {
                    Id = "probe-1",
                    MetricName = "method.duration",
                    Value = new SnapshotSegment("duration", valueJson, string.Empty),
                },
            },
        };
        var incoming = new ProbeConfiguration
        {
            MetricProbes = new[]
            {
                new MetricProbe
                {
                    Id = "probe-1",
                    MetricName = "method.duration",
                    Value = new SnapshotSegment("duration", valueJson, string.Empty),
                },
            },
        };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }

    [Fact]
    public void CurrentSpanDecorationsReparsedJsonContent_ProbeRelatedNotChanged()
    {
        const string whenJson = @"{""eq"":[{""ref"":""http.status_code""},500]}";
        const string tagValueJson = @"{""ref"":""error.message""}";

        var current = new ProbeConfiguration
        {
            SpanDecorationProbes = new[]
            {
                new SpanDecorationProbe
                {
                    Id = "probe-1",
                    Decorations = new[]
                    {
                        new Decoration
                        {
                            When = new SnapshotSegment("http.status_code == 500", whenJson, string.Empty),
                            Tags = new[]
                            {
                                new DebuggerTags
                                {
                                    Name = "error.message",
                                    Value = new TagValue
                                    {
                                        Template = "error={error.message}",
                                        Segments = new[]
                                        {
                                            new SnapshotSegment(string.Empty, null, "error="),
                                            new SnapshotSegment("error.message", tagValueJson, string.Empty),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
        var incoming = new ProbeConfiguration
        {
            SpanDecorationProbes = new[]
            {
                new SpanDecorationProbe
                {
                    Id = "probe-1",
                    Decorations = new[]
                    {
                        new Decoration
                        {
                            When = new SnapshotSegment("http.status_code == 500", whenJson, string.Empty),
                            Tags = new[]
                            {
                                new DebuggerTags
                                {
                                    Name = "error.message",
                                    Value = new TagValue
                                    {
                                        Template = "error={error.message}",
                                        Segments = new[]
                                        {
                                            new SnapshotSegment(string.Empty, null, "error="),
                                            new SnapshotSegment("error.message", tagValueJson, string.Empty),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        var comparer = new ProbeConfigurationComparer(current, incoming);
        comparer.HasProbeRelatedChanges.Should().BeFalse();
        comparer.HasRateLimitChanged.Should().BeFalse();
    }
}
