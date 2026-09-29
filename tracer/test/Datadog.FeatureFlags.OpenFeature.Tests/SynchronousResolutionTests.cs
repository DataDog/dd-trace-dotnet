// <copyright file="SynchronousResolutionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// These tests deliberately opt into the POC API. Production async callers need no suppression.
#pragma warning disable DDFF001

public class SynchronousResolutionTests
{
    [Fact]
    public async Task BooleanPreservesDefaultAndErrorWithoutInstrumentation()
    {
        using var provider = new DatadogProvider();
        var fallback = true;
        var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();
        var result = provider.ResolveBooleanValue("flag", fallback, context);
        var asyncResult = await provider.ResolveBooleanValueAsync("flag", fallback, context);

        Assert.Equal("flag", result.FlagKey);
        Assert.Equal(fallback, result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Equal(asyncResult.Value, result.Value);
        Assert.Equal(asyncResult.ErrorType, result.ErrorType);
        Assert.Equal(asyncResult.ErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task DoublePreservesDefaultAndErrorWithoutInstrumentation()
    {
        using var provider = new DatadogProvider();
        var fallback = 12.5;
        var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();
        var result = provider.ResolveDoubleValue("flag", fallback, context);
        var asyncResult = await provider.ResolveDoubleValueAsync("flag", fallback, context);

        Assert.Equal("flag", result.FlagKey);
        Assert.Equal(fallback, result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Equal(asyncResult.Value, result.Value);
        Assert.Equal(asyncResult.ErrorType, result.ErrorType);
        Assert.Equal(asyncResult.ErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task IntegerPreservesDefaultAndErrorWithoutInstrumentation()
    {
        using var provider = new DatadogProvider();
        var fallback = 42;
        var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();
        var result = provider.ResolveIntegerValue("flag", fallback, context);
        var asyncResult = await provider.ResolveIntegerValueAsync("flag", fallback, context);

        Assert.Equal("flag", result.FlagKey);
        Assert.Equal(fallback, result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Equal(asyncResult.Value, result.Value);
        Assert.Equal(asyncResult.ErrorType, result.ErrorType);
        Assert.Equal(asyncResult.ErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task StringPreservesDefaultAndErrorWithoutInstrumentation()
    {
        using var provider = new DatadogProvider();
        var fallback = "fallback";
        var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();
        var result = provider.ResolveStringValue("flag", fallback, context);
        var asyncResult = await provider.ResolveStringValueAsync("flag", fallback, context);

        Assert.Equal("flag", result.FlagKey);
        Assert.Equal(fallback, result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Equal(asyncResult.Value, result.Value);
        Assert.Equal(asyncResult.ErrorType, result.ErrorType);
        Assert.Equal(asyncResult.ErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task StructurePreservesDefaultAndErrorWithoutInstrumentation()
    {
        using var provider = new DatadogProvider();
        var fallback = new Value(Structure.Builder().Set("fallback", true).Build());
        var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();
        var result = provider.ResolveStructureValue("flag", fallback, context);
        var asyncResult = await provider.ResolveStructureValueAsync("flag", fallback, context);

        Assert.Equal("flag", result.FlagKey);
        Assert.Equal(fallback, result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Equal(asyncResult.Value, result.Value);
        Assert.Equal(asyncResult.ErrorType, result.ErrorType);
        Assert.Equal(asyncResult.ErrorMessage, result.ErrorMessage);
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
