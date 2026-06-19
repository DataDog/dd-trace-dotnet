// <copyright file="DuckTypeTaskTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

#pragma warning disable xUnit1031 // The test intentionally exercises the duck-typed awaiter GetResult contract.

public class DuckTypeTaskTests
{
    [Fact]
    public void NonGenericTaskDuckTypeExposesAwaiter()
    {
        var task = new Task(static () => { });
        task.RunSynchronously();

        var proxy = task.DuckCast<IDuckTypeTask>();
        var awaiter = proxy.GetAwaiter();

        awaiter.IsCompleted.Should().BeTrue();
        awaiter.GetResult();

        ((IDuckType)proxy).Instance.Should().BeSameAs(task);
        ((IDuckType)proxy).Type.Should().Be(task.GetType());
        ((IDuckType)awaiter).Type.Should().Be(typeof(TaskAwaiter));
    }

    [Fact]
    public void GenericTaskDuckTypeExposesResultAndAwaiter()
    {
        var task = Task.FromResult("completed");
        var proxy = task.DuckCast<IDuckTypeTask<string>>();
        var awaiter = proxy.GetAwaiter();

        proxy.Result.Should().Be("completed");
        awaiter.IsCompleted.Should().BeTrue();
        awaiter.GetResult().Should().Be("completed");

        ((IDuckType)proxy).Instance.Should().BeSameAs(task);
        ((IDuckType)proxy).Type.Should().Be(task.GetType());
        ((IDuckType)awaiter).Type.Should().Be(typeof(TaskAwaiter<string>));
    }
}

#pragma warning restore xUnit1031
