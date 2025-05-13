// <copyright file="DuckAsClassTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order

namespace Datadog.Trace.DuckTyping.Tests;

public class DuckAsClassTests
{
    [Fact]
    public void DuckAsInterfaceTest()
    {
        var targetObject = new TargetObject();
        var proxy = targetObject.DuckCast<IDuckAsInterface>();

        proxy.SayHi().Should().Be("Hello World");
        proxy.GetType().IsValueType.Should().BeTrue();
        proxy.GetType().IsClass.Should().BeFalse();
    }

    [Fact]
    public void DuckAsClassTest()
    {
        var targetObject = new TargetObject();
        var proxy = targetObject.DuckCast<IDuckAsClass>();

        proxy.SayHi().Should().Be("Hello World");
        proxy.GetType().IsValueType.Should().BeFalse();
        proxy.GetType().IsClass.Should().BeTrue();
    }

    public class TargetObject
    {
        public string SayHi() => "Hello World";
    }

    public interface IDuckAsInterface
    {
        string SayHi();
    }

    [DuckAsClass]
    public interface IDuckAsClass
    {
        string SayHi();
    }
}
