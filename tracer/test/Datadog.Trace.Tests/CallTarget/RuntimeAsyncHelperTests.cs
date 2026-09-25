// <copyright file="RuntimeAsyncHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Runtime-async only exists on .NET 10+, so there is no point exercising these helpers on any
// other target - the code paths they serve can never be reached there.
#if NET10_0_OR_GREATER

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

public class RuntimeAsyncHelperTests
{
    [Fact]
    public void CreateCompleted_ForTask_ReturnsTheCachedCompletedTask()
    {
        var completed = RuntimeAsyncHelper.CreateCompleted<Task>();

        completed.Should().BeSameAs(Task.CompletedTask);
        completed!.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void CreateCompleted_ForValueTask_ReturnsACompletedValueTask()
    {
        var completed = RuntimeAsyncHelper.CreateCompleted<ValueTask>();

        completed.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void CreateCompleted_ForAnUnrecognisedType_ReturnsDefault()
    {
        RuntimeAsyncHelper.CreateCompleted<string>().Should().BeNull();
        RuntimeAsyncHelper.CreateCompleted<int>().Should().Be(0);
    }

    [Fact]
    public async Task CreateCompletedFromResult_ForGenericTask_CarriesTheResult()
    {
        var completed = RuntimeAsyncHelper.CreateCompletedFromResult<int, Task<int>>(42);

        completed!.IsCompletedSuccessfully.Should().BeTrue();
        (await completed).Should().Be(42);
    }

    [Fact]
    public async Task CreateCompletedFromResult_ForGenericTask_AllowsANullResult()
    {
        var completed = RuntimeAsyncHelper.CreateCompletedFromResult<string, Task<string>>(null);

        completed!.IsCompletedSuccessfully.Should().BeTrue();
        (await completed).Should().BeNull();
    }

    // This branch is guarded by #if NETCOREAPP3_1_OR_GREATER in RuntimeAsyncHelper. A net10.0 test
    // binds the net6.0 build of Datadog.Trace - the same asset loaded in production on .NET 10/11 -
    // so the guard is satisfied and no #if is needed here.
    [Fact]
    public async Task CreateCompletedFromResult_ForGenericValueTask_CarriesTheResult()
    {
        var completed = RuntimeAsyncHelper.CreateCompletedFromResult<int, ValueTask<int>>(42);

        completed.IsCompletedSuccessfully.Should().BeTrue();
        (await completed).Should().Be(42);
    }

    [Fact]
    public async Task CreateCompletedFromResult_ForGenericValueTask_AllowsAReferenceResult()
    {
        var completed = RuntimeAsyncHelper.CreateCompletedFromResult<string, ValueTask<string>>("value");

        completed.IsCompletedSuccessfully.Should().BeTrue();
        (await completed).Should().Be("value");
    }

    [Fact]
    public void CreateCompletedFromResult_ForAnUnrecognisedDeclaredType_ReturnsDefault()
    {
        // The declared type has to match Task<TResult>/ValueTask<TResult> exactly. Anything else
        // fails soft rather than reinterpreting a value as the wrong type.
        RuntimeAsyncHelper.CreateCompletedFromResult<int, string>(42).Should().BeNull();
        RuntimeAsyncHelper.CreateCompletedFromResult<int, Task<string>>(42).Should().BeNull();
    }
}

#endif
