// <copyright file="SynchronousResolutionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// These tests deliberately opt into the POC API. Production async callers need no suppression.
#pragma warning disable DDFF001

public class SynchronousResolutionTests
{
    public static TheoryData<string, object> Fallbacks => new()
    {
        { "Boolean", true },
        { "Double", 12.5 },
        { "Integer", 42 },
        { "String", "fallback" },
        { "Structure", new Value(Structure.Builder().Set("fallback", true).Build()) },
    };

    [Theory]
    [MemberData(nameof(Fallbacks))]
    public async Task SyncMatchesAsyncWithoutInstrumentation(string type, object fallback)
    {
        using var provider = new DatadogProvider();
        var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();

        var (sync, asyncResult) = type switch
        {
            "Boolean" => ((object)provider.ResolveBooleanValue("flag", (bool)fallback, context), (object)await provider.ResolveBooleanValueAsync("flag", (bool)fallback, context)),
            "Double" => (provider.ResolveDoubleValue("flag", (double)fallback, context), await provider.ResolveDoubleValueAsync("flag", (double)fallback, context)),
            "Integer" => (provider.ResolveIntegerValue("flag", (int)fallback, context), await provider.ResolveIntegerValueAsync("flag", (int)fallback, context)),
            "String" => (provider.ResolveStringValue("flag", (string)fallback, context), await provider.ResolveStringValueAsync("flag", (string)fallback, context)),
            "Structure" => (provider.ResolveStructureValue("flag", (Value)fallback, context), await provider.ResolveStructureValueAsync("flag", (Value)fallback, context)),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        sync.Should().BeEquivalentTo(new { FlagKey = "flag", Value = fallback, ErrorType = ErrorType.ProviderNotReady }, o => o.ExcludingMissingMembers());
        sync.Should().BeEquivalentTo(asyncResult, o => o.RespectingRuntimeTypes());
    }

    [Theory]
    [InlineData("Boolean")]
    [InlineData("Double")]
    [InlineData("Integer")]
    [InlineData("String")]
    [InlineData("Structure")]
    public void CancellationIsCheckedBeforeResolution(string type)
    {
        using var provider = new DatadogProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // A null key would otherwise throw ArgumentNullException in the uninstrumented SDK.
        Assert.Throws<OperationCanceledException>(() => Resolve(provider, type, null!, cancellation.Token));
    }

    [Theory]
    [InlineData("Boolean")]
    [InlineData("Double")]
    [InlineData("Integer")]
    [InlineData("String")]
    [InlineData("Structure")]
    public void NullKeyRetainsProviderContract(string type)
    {
        using var provider = new DatadogProvider();
        Assert.Throws<ArgumentNullException>(() => Resolve(provider, type, null!, default));
    }

    [Theory]
    [InlineData("Boolean")]
    [InlineData("Double")]
    [InlineData("Integer")]
    [InlineData("String")]
    [InlineData("Structure")]
    public void OnlySyncMethodsAreExperimentalAndNeitherIsObsolete(string type)
    {
        var sync = typeof(DatadogProvider).GetMethod($"Resolve{type}Value")!;
        var asyncMethod = typeof(DatadogProvider).GetMethod($"Resolve{type}ValueAsync")!;
        const string attributeName = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";
        var attribute = Assert.Single(sync.CustomAttributes, a => a.AttributeType.FullName == attributeName);

        Assert.Equal("DDFF001", attribute.ConstructorArguments[0].Value);
        Assert.DoesNotContain(asyncMethod.CustomAttributes, a => a.AttributeType.FullName == attributeName);
        Assert.False(sync.IsDefined(typeof(ObsoleteAttribute), false));
        Assert.False(asyncMethod.IsDefined(typeof(ObsoleteAttribute), false));
    }

    private static object Resolve(DatadogProvider provider, string type, string key, CancellationToken cancellationToken) => type switch
    {
        "Boolean" => provider.ResolveBooleanValue(key, true, cancellationToken: cancellationToken),
        "Double" => provider.ResolveDoubleValue(key, 12.5, cancellationToken: cancellationToken),
        "Integer" => provider.ResolveIntegerValue(key, 42, cancellationToken: cancellationToken),
        "String" => provider.ResolveStringValue(key, "fallback", cancellationToken: cancellationToken),
        "Structure" => provider.ResolveStructureValue(key, new Value(), cancellationToken: cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

#pragma warning restore DDFF001
