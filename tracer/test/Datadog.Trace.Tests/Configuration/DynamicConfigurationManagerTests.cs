// <copyright file="DynamicConfigurationManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class DynamicConfigurationManagerTests
{
    private const string ProductName = DynamicConfigurationManager.ProductName;
    private static int _version;

    [Fact]
    public void CombineApmTracingConfiguration_WhenNoConfiguration_ReturnsEmptyCollection()
    {
        Dictionary<string, RemoteConfiguration> activeConfigs = new();
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new();
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new();
        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEmpty();
        applyDetails.Should().BeEmpty();
    }

    [Fact]
    public void CombineApmTracingConfiguration_WhenUnknownProducts_ReturnsEmptyCollection()
    {
        Dictionary<string, RemoteConfiguration> activeConfigs = new();
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new()
        {
            { "Something else", [CreateConfig()] },
            { "Blah", [CreateConfig()] },
        };
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new();
        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEmpty();
        AssertApplyDetails(applyDetails, activeConfigs, configByProduct, removedConfigByProduct);
    }

    [Fact]
    public void CombineApmTracingConfiguration_WhenNoConfigsByProduct_ReturnsEmptyCollection()
    {
        Dictionary<string, RemoteConfiguration> activeConfigs = new();
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new();
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new()
        {
            { ProductName, [RemoteConfigurationPath.FromPath("datadog/1/a/b/c"), RemoteConfigurationPath.FromPath("employee/1/2/3")] },
            { "Blep", [RemoteConfigurationPath.FromPath("datadog/1/d/b/c"), RemoteConfigurationPath.FromPath("employee/4/3/2")] },
        };

        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEmpty();
        AssertApplyDetails(applyDetails, activeConfigs, configByProduct, removedConfigByProduct);
    }

    [Fact]
    public void CombineApmTracingConfiguration_WhenExistingConfigIsRemoved_RemovesConfigAndLeavesRemaining()
    {
        const string pathToRemove = "datadog/1/a/some-random-id/c";
        const string expectedPath = "employee/1/other-ID/3";
        var configToRemove = CreateConfig(pathToRemove);
        var expected = CreateConfig(expectedPath);
        Dictionary<string, RemoteConfiguration> activeConfigs = new()
        {
            { configToRemove.Path.Id, CreateConfig(pathToRemove) },
            { expected.Path.Id, expected },
        };
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new();
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new()
        {
            { ProductName, [RemoteConfigurationPath.FromPath(pathToRemove), RemoteConfigurationPath.FromPath("datadog/2/dont/remove/me")] },
        };

        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEquivalentTo([expected]);
        AssertApplyDetails(applyDetails, activeConfigs, configByProduct, removedConfigByProduct);
    }

    [Fact]
    public void CombineApmTracingConfiguration_WhenNewConfigIsProvided_ReplacesExistingConfig()
    {
        const string pathToRemove = "datadog/1/a/some-random-id/c";
        const string expectedPath = "employee/1/other-ID/3";
        var configToRemove = CreateConfig(pathToRemove);
        var previous = CreateConfig(expectedPath);
        var updated = CreateConfig(expectedPath); // different config for same id
        Dictionary<string, RemoteConfiguration> activeConfigs = new()
        {
            { configToRemove.Path.Id, CreateConfig(pathToRemove) },
            { previous.Path.Id, previous },
        };
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new()
        {
            { ProductName, [updated] },
        };
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new()
        {
            { ProductName, [RemoteConfigurationPath.FromPath(pathToRemove), RemoteConfigurationPath.FromPath("datadog/2/dont/remove/me")] },
        };

        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEquivalentTo([updated]);
        AssertApplyDetails(applyDetails, activeConfigs, configByProduct, removedConfigByProduct);
    }

    [Fact]
    public void CombineApmTracingConfiguration_WhenNewConfigIsProvided_ReplacesExistingConfigRegardlessOfRemovedConfig()
    {
        const string pathToRemove = "datadog/1/a/some-random-id/c";
        const string expectedPath = "employee/1/other-ID/3";
        var configToRemove = CreateConfig(pathToRemove);
        var previous = CreateConfig(expectedPath);
        var updated = CreateConfig(expectedPath); // different config for same id
        Dictionary<string, RemoteConfiguration> activeConfigs = new()
        {
            { configToRemove.Path.Id, CreateConfig(pathToRemove) },
            { previous.Path.Id, previous },
        };
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new()
        {
            { ProductName, [updated] },
        };
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new()
        {
            // Note that we're _not_ explicitly removing the pathToRemove config, but as it's not in the "config by product", it gets removed
            { ProductName, [RemoteConfigurationPath.FromPath("datadog/2/dont/remove/me")] },
        };

        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEquivalentTo([updated]);
        AssertApplyDetails(applyDetails, activeConfigs, configByProduct, removedConfigByProduct);
    }

    [Fact]
    public void CombineApmTracingConfiguration_WhenNewConfigIsProvided_OverwritesNewAndExistingConfig()
    {
        const string pathToRemove = "datadog/1/a/some-random-id/c";
        const string expectedPath = "employee/1/other-ID/3";
        var configToRemove = CreateConfig(pathToRemove);
        var previous = CreateConfig(expectedPath);
        var updated1 = CreateConfig(expectedPath); // different configs for same id
        var updated2 = CreateConfig(expectedPath); // different configs for same id
        Dictionary<string, RemoteConfiguration> activeConfigs = new()
        {
            { configToRemove.Path.Id, CreateConfig(pathToRemove) },
            { previous.Path.Id, previous },
        };
        Dictionary<string, List<RemoteConfiguration>> configByProduct = new()
        {
            { ProductName, [updated1, updated2] },
        };
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct = new()
        {
            // Note that we're _not_ explicitly removing the pathToRemove config, but as it's not in the "config by product", it gets removed
            { ProductName, [RemoteConfigurationPath.FromPath("datadog/2/dont/remove/me")] },
        };

        List<ApplyDetails> applyDetails = [];
        var results = DynamicConfigurationManager.CombineApmTracingConfiguration(
            new(activeConfigs), // create a copy to avoid mutating the original
            configByProduct,
            removedConfigByProduct,
            applyDetails);

        results.Should().BeEquivalentTo([updated2]); // updated1 is replaced
        AssertApplyDetails(applyDetails, activeConfigs, configByProduct, removedConfigByProduct);
    }

    private static void AssertApplyDetails(
        List<ApplyDetails> applyDetails,
        Dictionary<string, RemoteConfiguration> originalConfig,
        Dictionary<string, List<RemoteConfiguration>> configByProduct,
        Dictionary<string, List<RemoteConfigurationPath>> removedConfigByProduct)
    {
        // Only acknowledge added configs, not removed ones
        var added = configByProduct
                   .Where(x => x.Key == ProductName)
                   .SelectMany(x => x.Value)
                   .Select(x => ApplyDetails.FromOk(x.Path.Path));
        List<ApplyDetails> expected = [..added];

        applyDetails.Should().BeEquivalentTo(expected);
    }

    private static RemoteConfiguration CreateConfig(string path = null)
    {
        var version = Interlocked.Increment(ref _version);
        path ??= $"datadog/{version}/product-{version}/id-{version}/path";
        return new RemoteConfiguration(
            RemoteConfigurationPath.FromPath(path),
            contents: [],
            length: 0,
            hashes: new(),
            version: Interlocked.Increment(ref _version)); // use version to create unique config values
    }
}
