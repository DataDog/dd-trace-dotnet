// <copyright file="ValueTaskHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

public class ValueTaskHelperTests
{
    [Fact]
    public void IsValueTask_WhenValueTask_ReturnsTrue()
    {
        var task = new ValueTask(Task.CompletedTask);
        Helper.IsValueTask(task).Should().BeTrue();
    }

    [Fact]
    public void IsValueTask_WhenGenericValueTask_ReturnsFalse()
    {
        var task = new ValueTask<bool>(Task.FromResult(true));
        Helper.IsValueTask(task).Should().BeFalse();
    }

    [Fact]
    public void IsValueTask_WhenTask_ReturnsFalse()
    {
        var task = Task.FromResult(true);
        Helper.IsValueTask(task).Should().BeFalse();
    }

    [Fact]
    public void IsGenericValueTask_WhenGenericValueTask_ReturnsTrue()
    {
        var task = new ValueTask<bool>(Task.FromResult(true));
        Helper.IsGenericTask(task).Should().BeTrue();
    }

    [Fact]
    public void IsGenericValueTask_WhenTask_ReturnsFalse()
    {
        var task = Task.FromResult(true);
        Helper.IsGenericTask(task).Should().BeFalse();
    }

    private static class Helper
    {
        public static bool IsValueTask<T>(T task)
        {
            return ValueTaskHelper.IsValueTask(typeof(T));
        }

        public static bool IsGenericTask<T>(T task)
        {
            return ValueTaskHelper.IsGenericValueTask(typeof(T));
        }
    }
}
