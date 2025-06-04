// <copyright file="ValueTaskActivatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

public class ValueTaskActivatorTests
{
    [Fact]
    public async Task NonGeneric_CreateTaskActivator_CanBeAwaited()
    {
        var activator = ValueTaskActivator<ValueTask>.CreateActivator();
        var task = Task.CompletedTask;

        var instance = activator(task);
        await instance;
    }

    [Fact]
    public async Task NonGeneric_FallbackActivator_CanBeAwaited()
    {
        var task = Task.CompletedTask;
        var instance = ValueTaskActivator<ValueTask>.FallbackActivator(task);
        await instance;
    }

    [Fact]
    public async Task Generic_CreateTaskActivator_CanBeAwaited()
    {
        var activator = ValueTaskActivator<ValueTask<int>, int>.CreateTaskActivator();
        var task = Task.FromResult(123);

        var instance = activator(task);
        var result = await instance;
        result.Should().Be(123);
    }

    [Fact]
    public async Task Generic_CreateResultActivator_CanBeAwaited()
    {
        var activator = ValueTaskActivator<ValueTask<int>, int>.CreateResultActivator();
        var value = 123;

        var instance = activator(value);
        var result = await instance;
        result.Should().Be(123);
    }

    [Fact]
    public async Task Generic_FallbackTaskActivator_CanBeAwaited()
    {
        var task = Task.FromResult(123);
        var instance = ValueTaskActivator<ValueTask<int>, int>.FallbackTaskActivator(task);
        var result = await instance;
        result.Should().Be(123);
    }

    [Fact]
    public async Task Generic_FallbackResultActivator_CanBeAwaited()
    {
        var value = 123;
        var instance = ValueTaskActivator<ValueTask<int>, int>.FallbackResultActivator(value);
        var result = await instance;
        result.Should().Be(123);
    }
}
#endif
