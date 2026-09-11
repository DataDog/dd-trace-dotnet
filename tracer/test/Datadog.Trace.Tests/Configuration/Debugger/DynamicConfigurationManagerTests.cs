// <copyright file="DynamicConfigurationManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.RemoteConfigurationManagement;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration;

public class DynamicConfigurationManagerTests
{
    private const string ProductName = DynamicConfigurationManager.ProductName;
    private static int _version;

    public enum DynamicDebuggerProduct
    {
        DynamicInstrumentation,
        ExceptionReplay,
        CodeOrigin
    }

    public static IEnumerable<object[]> DynamicDebuggerProductTransitions()
    {
        foreach (var product in new[] { DynamicDebuggerProduct.DynamicInstrumentation, DynamicDebuggerProduct.ExceptionReplay, DynamicDebuggerProduct.CodeOrigin })
        {
            yield return new object[] { product, null, null, false, false };
            yield return new object[] { product, null, false, false, false };
            yield return new object[] { product, null, true, false, true };
            yield return new object[] { product, false, null, false, false };
            yield return new object[] { product, false, false, false, false };
            yield return new object[] { product, false, true, false, true };
            yield return new object[] { product, true, null, false, true };
            yield return new object[] { product, true, false, false, true };
            yield return new object[] { product, true, true, false, false };

            // A null/empty RC payload does not dispose an already-running product. The next explicit
            // false must still be applied even though the old DynamicSettings value is null.
            yield return new object[] { product, null, false, true, true };
        }
    }

    public static IEnumerable<object[]> DynamicDebuggerProductTransitionSequences()
    {
        foreach (var product in new[] { DynamicDebuggerProduct.DynamicInstrumentation, DynamicDebuggerProduct.ExceptionReplay, DynamicDebuggerProduct.CodeOrigin })
        {
            yield return new object[] { product, new bool?[] { true, null, false }, new[] { true, true, true } };
            yield return new object[] { product, new bool?[] { null, false, true }, new[] { false, false, true } };
            yield return new object[] { product, new bool?[] { false, null, true }, new[] { false, false, true } };
            yield return new object[] { product, new bool?[] { true, false, null }, new[] { true, true, false } };
            yield return new object[] { product, new bool?[] { false, true, false }, new[] { false, true, true } };
            yield return new object[] { product, new bool?[] { null, true, null, false }, new[] { false, true, true, true } };
        }
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenDynamicSettingsUnchanged_ReturnsFalse()
    {
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: false);

        // Old settings have the default DynamicSettings (all null); new settings are also all null
        var newDynamicSettings = new ImmutableDynamicDebuggerSettings();

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenAllProductsOffAndNewDynamicAllOff_ReturnsFalse()
    {
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: false);

        // Explicit false for all three relevant products. Even though this differs from the
        // default-null DynamicSettings, nothing was running and nothing will run, so we skip.
        var newDynamicSettings = new ImmutableDynamicDebuggerSettings
        {
            DynamicInstrumentationEnabled = false,
            ExceptionReplayEnabled = false,
            CodeOriginEnabled = false,
        };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenAllProductsOffAndNewDynamicEnablesProduct_ReturnsTrue()
    {
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: false);
        var newDynamicSettings = new ImmutableDynamicDebuggerSettings
        {
            DynamicInstrumentationEnabled = true,
        };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenProductWasOnViaEnvAndNewDynamicDisablesIt_ReturnsTrue()
    {
        // env enables DI; we must always proceed so the manager can disable it
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: true, coEnabled: false, symDbEnabled: false);
        var newDynamicSettings = new ImmutableDynamicDebuggerSettings
        {
            DynamicInstrumentationEnabled = false,
        };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenExceptionReplayWasOnViaEnvAndNewDynamicAllOff_ReturnsTrue()
    {
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: false);
        var newDynamicSettings = new ImmutableDynamicDebuggerSettings
        {
            ExceptionReplayEnabled = false,
        };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: true)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenProductOnViaPreviousDynamicAndNewDynamicDisablesIt_ReturnsTrue()
    {
        var baseSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: false);

        // Simulate previously-applied dynamic config that turned DI on
        var oldDebuggerSettings = baseSettings with
        {
            DynamicSettings = new ImmutableDynamicDebuggerSettings { DynamicInstrumentationEnabled = true },
        };

        var newDynamicSettings = new ImmutableDynamicDebuggerSettings { DynamicInstrumentationEnabled = false };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(DynamicDebuggerProductTransitions))]
    public void ShouldApplyDynamicDebuggerConfig_ForDynamicDebuggerProductTransitions_ReturnsExpected(
        DynamicDebuggerProduct product,
        bool? previousDynamicValue,
        bool? newDynamicValue,
        bool hasActiveDynamicDebuggerProduct,
        bool expected)
    {
        var baseSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: false);
        var oldDebuggerSettings = baseSettings with { DynamicSettings = CreateDynamicSettings(product, previousDynamicValue) };
        var newDynamicSettings = CreateDynamicSettings(product, newDynamicValue);

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false, hasActiveDynamicDebuggerProduct)
            .Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(DynamicDebuggerProductTransitionSequences))]
    public void ShouldApplyDynamicDebuggerConfig_ForDynamicDebuggerProductTransitionSequences_ReturnsExpected(
        DynamicDebuggerProduct product,
        bool?[] sequence,
        bool[] expectedApplyResults)
    {
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: true);
        var activeProduct = false;
        var actualApplyResults = new bool[sequence.Length];

        for (var i = 0; i < sequence.Length; i++)
        {
            var newDynamicSettings = CreateDynamicSettings(product, sequence[i]);
            var shouldApply = DynamicConfigurationManager.ShouldApplyDynamicDebuggerConfig(
                oldDebuggerSettings,
                newDynamicSettings,
                exceptionReplayEnvEnabled: false,
                hasActiveDynamicDebuggerProduct: activeProduct);
            actualApplyResults[i] = shouldApply;

            if (shouldApply)
            {
                oldDebuggerSettings = oldDebuggerSettings with { DynamicSettings = newDynamicSettings };

                if (sequence[i] == true)
                {
                    activeProduct = true;
                }
                else if (sequence[i] == false)
                {
                    activeProduct = false;
                }
            }
        }

        actualApplyResults.Should().Equal(expectedApplyResults);
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenOnlySymDbIsOnAndNewDynamicAllOff_ReturnsFalse()
    {
        // SymDB is gated separately by SymDbRemoteConfig and is intentionally not part of
        // IsAnyRelevantProductRequested. With SymDB on but DI/ER/CO all off, an APM_TRACING
        // dynamic config that doesn't enable any DI/ER/CO product is a no-op for the manager,
        // so we skip applying it. This test pins that intent so a future refactor of the gate
        // can't silently regress the SymDB-only path that motivated this skip.
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: false, symDbEnabled: true);

        var newDynamicSettings = new ImmutableDynamicDebuggerSettings
        {
            DynamicInstrumentationEnabled = false,
            ExceptionReplayEnabled = false,
            CodeOriginEnabled = false,
        };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyDynamicDebuggerConfig_WhenCodeOriginOnViaEnvAndNewDynamicAllOff_ReturnsTrue()
    {
        // CO is on via env; the new dynamic config differs from the default and may need to
        // disable CO, so we must proceed with the apply.
        var oldDebuggerSettings = CreateDebuggerSettings(diEnabled: false, coEnabled: true, symDbEnabled: false);
        var newDynamicSettings = new ImmutableDynamicDebuggerSettings { CodeOriginEnabled = false };

        DynamicConfigurationManager
            .ShouldApplyDynamicDebuggerConfig(oldDebuggerSettings, newDynamicSettings, exceptionReplayEnvEnabled: false)
            .Should().BeTrue();
    }

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

    private static DebuggerSettings CreateDebuggerSettings(bool diEnabled, bool coEnabled, bool symDbEnabled)
        => new(
            new DictionaryConfigurationSource(new Dictionary<string, string>
            {
                { ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, diEnabled ? "true" : "false" },
                { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, coEnabled ? "true" : "false" },
                { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, symDbEnabled ? "true" : "false" },
            }),
            NullConfigurationTelemetry.Instance);

    private static ImmutableDynamicDebuggerSettings CreateDynamicSettings(DynamicDebuggerProduct product, bool? value)
    {
        return product switch
        {
            DynamicDebuggerProduct.DynamicInstrumentation => new ImmutableDynamicDebuggerSettings { DynamicInstrumentationEnabled = value },
            DynamicDebuggerProduct.ExceptionReplay => new ImmutableDynamicDebuggerSettings { ExceptionReplayEnabled = value },
            DynamicDebuggerProduct.CodeOrigin => new ImmutableDynamicDebuggerSettings { CodeOriginEnabled = value },
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, null)
        };
    }
}
