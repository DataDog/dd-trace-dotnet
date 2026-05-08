// <copyright file="ConfigurationUpdaterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ConfigurationUpdaterTests
{
    [Fact]
    public void MergeProbes_FileAndRcm_UnionOfIds()
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
    public void MergeProbes_DuplicateIds_RcmWins()
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
    public void AcceptFile_FiltersByLanguageAndMaxProbeCount()
    {
        var updater = CreateUpdater(maxProbesPerType: 1, out var addedProbes, out _);

        var result = updater.AcceptFile(
            new ProbeConfiguration
            {
                LogProbes =
                [
                    CreateLogProbe("dotnet-probe-1"),
                    CreateLogProbe("dotnet-probe-2"),
                    CreateLogProbe("java-probe", language: "java"),
                ]
            });

        result.Select(r => r.Id).Should().Equal("dotnet-probe-1");
        addedProbes.Should().ContainSingle();
        addedProbes[0].Select(p => p.Id).Should().Equal("dotnet-probe-1");
        GetCurrentConfiguration(updater).LogProbes.Select(p => p.Id).Should().Equal("dotnet-probe-1");
    }

    [Fact]
    public void AcceptAdded_RcmProbeOverridesFileProbeWithSameId()
    {
        var updater = CreateUpdater(out var addedProbes, out _);
        updater.AcceptFile(
            new ProbeConfiguration
            {
                LogProbes = [CreateLogProbe("shared-id", "From file", sourceFile: "file.cs")]
            });
        addedProbes.Clear();

        var result = updater.AcceptAdded(
            new ProbeConfiguration
            {
                LogProbes = [CreateLogProbe("shared-id", "From RCM", sourceFile: "rcm.cs")]
            });

        result.Select(r => r.Id).Should().Equal("shared-id");
        addedProbes.Should().ContainSingle();
        var addedProbe = addedProbes[0].Should().ContainSingle().Which.Should().BeOfType<LogProbe>().Which;
        addedProbe.Template.Should().Be("From RCM");
        addedProbe.Where.SourceFile.Should().Be("rcm.cs");
        GetCurrentConfiguration(updater).LogProbes.Should().ContainSingle().Which.Template.Should().Be("From RCM");
    }

    [Fact]
    public void AcceptRemoved_RemovesRcmProbeAndSuppressesFileProbeWithSameId()
    {
        var updater = CreateUpdater(out _, out var removedProbeIds);
        var fileConfiguration = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("shared-id", "From file", sourceFile: "file.cs")]
        };

        updater.AcceptFile(fileConfiguration);
        updater.AcceptAdded(
            new ProbeConfiguration
            {
                LogProbes = [CreateLogProbe("shared-id", "From RCM", sourceFile: "rcm.cs")]
            });

        updater.AcceptRemoved([CreateRcmPath(DefinitionPaths.LogProbe, "shared-id")]);

        removedProbeIds.Should().Equal("shared-id");
        GetCurrentConfiguration(updater).LogProbes.Should().BeEmpty();
        updater.HasAnyEffectiveProbeForFile(fileConfiguration).Should().BeFalse();
    }

    [Fact]
    public void AcceptAdded_AfterRemovalClearsFileProbeSuppression()
    {
        var updater = CreateUpdater(out _, out _);
        var fileConfiguration = new ProbeConfiguration
        {
            LogProbes = [CreateLogProbe("shared-id", "From file", sourceFile: "file.cs")]
        };

        updater.AcceptFile(fileConfiguration);
        updater.AcceptAdded(
            new ProbeConfiguration
            {
                LogProbes = [CreateLogProbe("shared-id", "From RCM", sourceFile: "rcm.cs")]
            });
        updater.AcceptRemoved([CreateRcmPath(DefinitionPaths.LogProbe, "shared-id")]);

        updater.AcceptAdded(
            new ProbeConfiguration
            {
                LogProbes = [CreateLogProbe("shared-id", "From RCM again", sourceFile: "rcm.cs")]
            });

        updater.HasAnyEffectiveProbeForFile(fileConfiguration).Should().BeTrue();
        GetCurrentConfiguration(updater).LogProbes.Should().ContainSingle().Which.Template.Should().Be("From RCM again");
    }

    private static ConfigurationUpdater CreateUpdater(
        out List<IReadOnlyList<ProbeDefinition>> addedProbes,
        out List<string> removedProbeIds)
    {
        return CreateUpdater(maxProbesPerType: 0, out addedProbes, out removedProbeIds);
    }

    private static ConfigurationUpdater CreateUpdater(
        int maxProbesPerType,
        out List<IReadOnlyList<ProbeDefinition>> addedProbes,
        out List<string> removedProbeIds)
    {
        addedProbes = [];
        removedProbeIds = [];

        var addedProbesCapture = addedProbes;
        var removedProbeIdsCapture = removedProbeIds;
        var updater = ConfigurationUpdater.Create("env", "version", maxProbesPerType);
        updater.SetProbeInstrumentationHandlers(
            probes =>
            {
                addedProbesCapture.Add(probes);
                return probes.Select(probe => new ConfigurationUpdater.UpdateResult(probe.Id, null)).ToList();
            },
            probeIds => removedProbeIdsCapture.AddRange(probeIds));

        return updater;
    }

    private static LogProbe CreateLogProbe(string id, string? template = null, string sourceFile = "file.cs", string language = "dotnet")
    {
        return new LogProbe
        {
            Id = id,
            Language = language,
            Template = template ?? id,
            Where = new Where { SourceFile = sourceFile, Lines = ["10"] },
        };
    }

    private static RemoteConfigurationPath CreateRcmPath(string probePrefix, string probeId)
    {
        return RemoteConfigurationPath.FromPath($"employee/{RcmProducts.LiveDebugging}/{probePrefix}{probeId}/config");
    }

    private static ProbeConfiguration GetCurrentConfiguration(ConfigurationUpdater updater)
    {
        var field = typeof(ConfigurationUpdater).GetField("_currentConfiguration", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (ProbeConfiguration)field!.GetValue(updater)!;
    }
}
