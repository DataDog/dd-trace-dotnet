// <copyright file="ProbeConfigurationUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Linq;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeConfigurationUtilsTests
{
    public static TheoryData<string> ProbePathPrefixes() => new()
    {
        DefinitionPaths.LogProbe,
        DefinitionPaths.MetricProbe,
        DefinitionPaths.SpanProbe,
        DefinitionPaths.SpanDecorationProbe,
    };

    [Theory]
    [MemberData(nameof(ProbePathPrefixes))]
    public void GetProbeIdFromPath_ReturnsIdWithoutProbePrefix(string probePrefix)
    {
        var path = CreateRcmPath(probePrefix, "probe-id");

        ProbeConfigurationUtils.GetProbeIdFromPath(path).Should().Be("probe-id");
    }

    [Fact]
    public void GetProbeIdFromPath_ReturnsWholeIdForNonProbePath()
    {
        var path = CreateRcmPath(DefinitionPaths.ServiceConfiguration, "service");

        ProbeConfigurationUtils.GetProbeIdFromPath(path).Should().Be("serviceConfig_service");
    }

    [Theory]
    [MemberData(nameof(ProbePathPrefixes))]
    public void IsProbePath_ReturnsTrueForProbePaths(string probePrefix)
    {
        ProbeConfigurationUtils.IsProbePath(CreateRcmPath(probePrefix, "probe-id")).Should().BeTrue();
    }

    [Fact]
    public void IsProbePath_ReturnsFalseForNonProbePath()
    {
        ProbeConfigurationUtils.IsProbePath(CreateRcmPath(DefinitionPaths.ServiceConfiguration, "service")).Should().BeFalse();
    }

    [Fact]
    public void IsProbeId_MatchesProbePathWithoutAllocatingExtractedId()
    {
        var path = CreateRcmPath(DefinitionPaths.LogProbe, "probe-id");

        ProbeConfigurationUtils.IsProbeId(path, "probe-id").Should().BeTrue();
        ProbeConfigurationUtils.IsProbeId(path, "probe").Should().BeFalse();
        ProbeConfigurationUtils.IsProbeId(path, "probe-id-extra").Should().BeFalse();
    }

    [Fact]
    public void GetProbeIds_ReturnsAllProbeIdsInConfigurationOrder()
    {
        var configuration = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("log-id")],
            MetricProbes = [CreateMetricProbe("metric-id")],
            SpanProbes = [CreateSpanProbe("span-id")],
            SpanDecorationProbes = [CreateSpanDecorationProbe("span-decoration-id")]
        };

        ProbeConfigurationUtils.GetProbeIds(configuration).Should().Equal("log-id", "metric-id", "span-id", "span-decoration-id");
    }

    [Fact]
    public void Merge_WhenLowerPriorityIsNull_ReturnsHigherPriorityProbes()
    {
        var higherPriorityLogProbes = new[] { CreateLogProbe("rcm-probe") };
        var higherPriority = new ProbeConfiguration
        {
            LogProbes = higherPriorityLogProbes
        };

        var merged = ProbeConfigurationUtils.Merge(null, higherPriority);

        merged.LogProbes.Should().BeSameAs(higherPriorityLogProbes);
    }

    [Fact]
    public void Merge_WhenHigherPriorityHasNoProbes_ReturnsLowerPriorityProbes()
    {
        var lowerPriorityLogProbes = new[] { CreateLogProbe("file-probe") };
        var lowerPriority = new ProbeConfiguration
        {
            LogProbes = lowerPriorityLogProbes
        };

        var merged = ProbeConfigurationUtils.Merge(lowerPriority, new ProbeConfiguration());

        merged.LogProbes.Should().BeSameAs(lowerPriorityLogProbes);
    }

    [Fact]
    public void Merge_FileAndRcm_ReturnsUnionOfProbeIds()
    {
        var fileProbes = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("file-probe-1")]
        };

        var rcmProbes = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("rcm-probe-1")]
        };

        var merged = ProbeConfigurationUtils.Merge(fileProbes, rcmProbes);

        merged.LogProbes.Select(p => p.Id).Should().BeEquivalentTo("file-probe-1", "rcm-probe-1");
    }

    [Fact]
    public void Merge_DuplicateIds_HigherPriorityWins()
    {
        var fileProbes = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("shared-id", "From file", sourceFile: "file.cs")]
        };

        var rcmProbes = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("shared-id", "From RCM", sourceFile: "rcm.cs")]
        };

        var merged = ProbeConfigurationUtils.Merge(fileProbes, rcmProbes);

        merged.LogProbes.Should().ContainSingle();
        merged.LogProbes[0].Template.Should().Be("From RCM");
        merged.LogProbes[0].Where.SourceFile.Should().Be("rcm.cs");
    }

    [Fact]
    public void RemoveItems_WhenNothingIsRemoved_ReturnsSameConfiguration()
    {
        var configuration = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("log-id")]
        };

        ProbeConfigurationUtils.RemoveItems(configuration, [], removeServiceConfiguration: false).Should().BeSameAs(configuration);
    }

    [Fact]
    public void RemoveItems_WhenRemovedIdsDoNotMatch_ReusesProbeArrays()
    {
        var logProbes = new[] { CreateLogProbe("log-id") };
        var configuration = new ProbeConfiguration
        {
            LogProbes = logProbes
        };

        var result = ProbeConfigurationUtils.RemoveItems(configuration, ["other-id"], removeServiceConfiguration: false);

        result.Should().NotBeSameAs(configuration);
        result.LogProbes.Should().BeSameAs(logProbes);
    }

    [Fact]
    public void RemoveItems_RemovesMatchingProbeIdsAcrossProbeTypes()
    {
        var configuration = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("remove-id"), CreateLogProbe("keep-log")],
            MetricProbes = [CreateMetricProbe("remove-id"), CreateMetricProbe("keep-metric")],
            SpanProbes = [CreateSpanProbe("remove-id"), CreateSpanProbe("keep-span")],
            SpanDecorationProbes = [CreateSpanDecorationProbe("remove-id"), CreateSpanDecorationProbe("keep-span-decoration")]
        };

        var result = ProbeConfigurationUtils.RemoveItems(configuration, ["remove-id"], removeServiceConfiguration: false);

        result.LogProbes.Select(p => p.Id).Should().Equal("keep-log");
        result.MetricProbes.Select(p => p.Id).Should().Equal("keep-metric");
        result.SpanProbes.Select(p => p.Id).Should().Equal("keep-span");
        result.SpanDecorationProbes.Select(p => p.Id).Should().Equal("keep-span-decoration");
    }

    [Fact]
    public void RemoveItems_CanRemoveServiceConfigurationWithoutChangingProbes()
    {
        var logProbes = new[] { CreateLogProbe("log-id") };
        var serviceConfiguration = new ServiceConfiguration();
        var configuration = new ProbeConfiguration
        {
            ServiceConfiguration = serviceConfiguration,
            LogProbes = logProbes
        };

        var result = ProbeConfigurationUtils.RemoveItems(configuration, [], removeServiceConfiguration: true);

        result.ServiceConfiguration.Should().BeNull();
        result.LogProbes.Should().BeSameAs(logProbes);
    }

    private static LogProbe CreateLogProbe(string id, string? template = null, string sourceFile = "file.cs")
    {
        return new LogProbe
        {
            Id = id,
            Language = "dotnet",
            Template = template ?? id,
            Where = new Where { SourceFile = sourceFile, Lines = ["10"] },
        };
    }

    private static MetricProbe CreateMetricProbe(string id)
    {
        return new MetricProbe
        {
            Id = id,
            Language = "dotnet",
            MetricName = id,
            Where = new Where { TypeName = "MyClass", MethodName = "MyMethod" },
        };
    }

    private static SpanProbe CreateSpanProbe(string id)
    {
        return new SpanProbe
        {
            Id = id,
            Language = "dotnet",
            Where = new Where { TypeName = "MyClass", MethodName = "MyMethod" },
        };
    }

    private static SpanDecorationProbe CreateSpanDecorationProbe(string id)
    {
        return new SpanDecorationProbe
        {
            Id = id,
            Language = "dotnet",
            Where = new Where { TypeName = "MyClass", MethodName = "MyMethod" },
            Decorations = [],
        };
    }

    private static RemoteConfigurationPath CreateRcmPath(string probePrefix, string probeId)
    {
        return RemoteConfigurationPath.FromPath($"employee/{RcmProducts.LiveDebugging}/{probePrefix}{probeId}/config");
    }
}
