// <copyright file="PropertyOrFieldTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class PropertyOrFieldTests
{
    [Fact]
    public void PropertyOrFieldTestUsesPropertyIfAvailable()
    {
        var target = new TargetWithProperty();
        var proxy = target.DuckCast<Proxy>();

        proxy.Value.Should().Be(target.Value);
    }

    [Fact]
    public void PropertyOrFieldTestUsesFieldIfAvailable()
    {
        var target = new TargetWithField();
        var proxy = target.DuckCast<Proxy>();

        proxy.Value.Should().Be(target.Value);
    }

    [Fact]
    public void PropertyOrFieldTestUsesNameFromDuckAttribute()
    {
        var target = new TargetWithFieldAndProperty();
        var proxy = target.DuckCast<ProxyWithName>();

        proxy.Value.Should().Be(target.GetValue());
    }

    [DuckCopy]
    public struct Proxy
    {
        [DuckPropertyOrField]
        public int Value;
    }

    [DuckCopy]
    public struct ProxyWithName
    {
        [DuckPropertyOrField(Name = "value")]
        public int Value;
    }

    public class TargetWithProperty
    {
        public int Value { get; } = 1;
    }

    public class TargetWithField
    {
#pragma warning disable SA1401 // should be private
        public int Value = 1;
#pragma warning restore SA1401
    }

    public class TargetWithFieldAndProperty
    {
        private int value = 3;

        public int Value { get; } = 4;

        public int GetValue() => value;
    }
}
