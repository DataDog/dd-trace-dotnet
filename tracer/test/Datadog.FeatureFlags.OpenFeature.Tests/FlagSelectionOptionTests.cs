// <copyright file="FlagSelectionOptionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class FlagSelectionOptionTests
{
    [Fact]
    public void TheConstructorHandsACopyOfTheSelectionToTheTracer()
    {
        var tags = new List<string> { "team:payments", "mobile" };
        string[] keyPatterns = ["checkout.*"];
        var options = new DatadogProviderOptions { FlagSelection = new FlagSelection { Tags = tags, KeyPatterns = keyPatterns } };
        var bridge = new BridgeRecorder();

        using var provider = new DatadogProvider(options, bridge.Set, bridge.Release);
        tags.Add("added-later");
        keyPatterns[0] = "changed-later";
        options.FlagSelection.Tags = ["replaced-later"];
        options.FlagSelection = null;

        bridge.Calls.Should().ContainSingle();
        bridge.Calls[0].Tags.Should().Equal("team:payments", "mobile");
        bridge.Calls[0].KeyPatterns.Should().Equal("checkout.*");
        bridge.Calls[0].KeyPatterns.Should().NotBeSameAs(keyPatterns);
    }

    [Fact]
    public void AProviderWithoutASelectionStillClaims()
    {
        var bridge = new BridgeRecorder();

        using var provider = new DatadogProvider(new DatadogProviderOptions(), bridge.Set, bridge.Release);

        bridge.Calls.Should().ContainSingle().Which.Should().Be((null, null));
    }

    [Fact]
    public void ANullListIsAbsent()
    {
        var bridge = new BridgeRecorder();

        using var provider = new DatadogProvider(new DatadogProviderOptions { FlagSelection = new FlagSelection { KeyPatterns = ["checkout.*"] } }, bridge.Set, bridge.Release);

        bridge.Calls[0].Tags.Should().BeNull();
        bridge.Calls[0].KeyPatterns.Should().Equal("checkout.*");
    }

    [Fact]
    public void NullElementsReachTheTracerToBeRejected()
    {
        var bridge = new BridgeRecorder();

        using var provider = new DatadogProvider(new DatadogProviderOptions { FlagSelection = new FlagSelection { Tags = ["mobile", null!] } }, bridge.Set, bridge.Release);

        bridge.Calls[0].Tags.Should().Equal("mobile", null);
    }

    [Fact]
    public void TheOptionsConstructorRequiresOptions()
        => FluentActions.Invoking(() => new DatadogProvider((DatadogProviderOptions)null!))
                        .Should().Throw<ArgumentNullException>().WithParameterName("options");

    [Fact]
    public void WithoutATracerThatSupportsItTheSelectionIsIgnored()
    {
        FeatureFlagsSdk.SetFlagSelection(["mobile"], null).Should().BeFalse("an older tracer leaves the stub's default body in place");

        var create = () => new DatadogProvider(new DatadogProviderOptions { FlagSelection = new FlagSelection { Tags = ["mobile"] } });

        create.Should().NotThrow().Which.Dispose();
    }

    [Fact]
    public async Task ShutdownReleasesTheClaimOnce()
    {
        var bridge = new BridgeRecorder();
        using var provider = new DatadogProvider(new DatadogProviderOptions(), bridge.Set, bridge.Release);

        await provider.ShutdownAsync();
        await provider.ShutdownAsync();

        bridge.Releases.Should().Be(1);
    }

    private sealed class BridgeRecorder
    {
        public List<(string?[]? Tags, string?[]? KeyPatterns)> Calls { get; } = [];

        public int Releases { get; private set; }

        public bool Set(string[]? tags, string[]? keyPatterns)
        {
            Calls.Add((tags, keyPatterns));
            return true;
        }

        public void Release() => Releases++;
    }
}
