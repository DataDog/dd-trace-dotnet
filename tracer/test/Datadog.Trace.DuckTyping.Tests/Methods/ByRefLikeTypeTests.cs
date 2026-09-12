// <copyright file="ByRefLikeTypeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Methods;

#pragma warning disable SA1201 // Elements should appear in the correct order

public class ByRefLikeTypeTests
{
    [Fact]
    public void ExactByRefLikeReturnCanBeInvoked()
    {
        var target = new ByRefLikeTarget();
        var proxy = target.DuckCast<IExactSpanReturnProxy>();

        proxy.GetSpan()[0] = 42;

        target.FirstValue.Should().Be(42);
    }

    [Fact]
    public void ByRefLikeReturnAsObjectIsRejected()
    {
        var target = new ByRefLikeTarget();

        DuckType.CanCreate<ISpanReturnAsObjectProxy>(target).Should().BeFalse();
        target.DuckIs<ISpanReturnAsObjectProxy>().Should().BeFalse();
    }

    [Fact]
    public void ByRefLikePropertyAsObjectIsRejected()
    {
        var target = new ByRefLikeTarget();

        DuckType.CanCreate<ISpanPropertyAsObjectProxy>(target).Should().BeFalse();
        target.DuckIs<ISpanPropertyAsObjectProxy>().Should().BeFalse();
    }

    [Fact]
    public void ObjectParameterCannotBeConvertedToByRefLikeType()
    {
        var target = new ByRefLikeTarget();

        DuckType.CanCreate<IObjectParameterProxy>(target).Should().BeFalse();
        target.DuckIs<IObjectParameterProxy>().Should().BeFalse();
    }

    [Fact]
    public void ByRefLikeParameterCannotBeBoxedAsObject()
    {
        var target = new ObjectParameterTarget();

        DuckType.CanCreate<ISpanParameterProxy>(target).Should().BeFalse();
        target.DuckIs<ISpanParameterProxy>().Should().BeFalse();
    }

    [Fact]
    public void ByRefLikeTypeInsideManagedReferenceCannotBeConverted()
    {
        var byRefSpan = typeof(Span<int>).MakeByRefType();
        var byRefObject = typeof(object).MakeByRefType();

        ILHelpersExtensions.CheckTypeConversion(byRefSpan, byRefObject).Should().NotBeNull();
        ILHelpersExtensions.CheckTypeConversion(byRefObject, byRefSpan).Should().NotBeNull();
    }

    private interface IExactSpanReturnProxy
    {
        Span<int> GetSpan();
    }

    private interface ISpanReturnAsObjectProxy
    {
        object GetSpan();
    }

    private interface ISpanPropertyAsObjectProxy
    {
        object Values { get; }
    }

    private interface IObjectParameterProxy
    {
        void Accept(object value);
    }

    private interface ISpanParameterProxy
    {
        void Accept(Span<int> value);
    }

    private class ByRefLikeTarget
    {
        private readonly int[] _values = [21];

        public int FirstValue => _values[0];

        public Span<int> Values => _values;

        public Span<int> GetSpan() => _values;

        public void Accept(Span<int> value)
        {
        }
    }

    private class ObjectParameterTarget
    {
        public void Accept(object value)
        {
        }
    }
}

#endif
