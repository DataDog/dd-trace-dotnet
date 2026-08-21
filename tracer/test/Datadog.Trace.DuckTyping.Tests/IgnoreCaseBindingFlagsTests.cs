// <copyright file="IgnoreCaseBindingFlagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements must appear in the correct order

namespace Datadog.Trace.DuckTyping.Tests;

public class IgnoreCaseBindingFlagsTests
{
    [Fact]
    public void InterfaceProxyBindsToADifferentlyCasedProperty()
    {
        object target = new LowerCasePropertyTarget();

        var proxy = target.DuckCast<IIgnoreCaseProxy>();

        proxy.Value.Should().Be("ok");
    }

    [Fact]
    public void DuckCopyProxyBindsToADifferentlyCasedProperty()
    {
        object target = new LowerCasePropertyTarget();

        var copy = target.DuckCast<IgnoreCaseCopyProxy>();

        copy.Value.Should().Be("ok");
    }

    [Fact]
    public void WithoutTheFlagADifferentlyCasedPropertyDoesNotBind()
    {
        // The negative control: without IgnoreCase the same pair must not resolve, so the tests above are
        // measuring the flag and not some incidental match.
        object target = new LowerCasePropertyTarget();

        target.DuckIs<ICaseSensitiveProxy>().Should().BeFalse();
    }

    public interface IIgnoreCaseProxy
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        string Value { get; }
    }

    public interface ICaseSensitiveProxy
    {
        string Value { get; }
    }

    [DuckCopy]
    public struct IgnoreCaseCopyProxy
    {
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public string Value;
    }

#pragma warning disable SA1300 // Element should begin with upper-case letter - the casing is the point
    internal class LowerCasePropertyTarget
    {
        public string value => "ok";
    }
#pragma warning restore SA1300
}
