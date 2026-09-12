// <copyright file="GenericMethodConstraintTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Methods;

#pragma warning disable SA1201 // Elements should appear in the correct order

public class GenericMethodConstraintTests
{
    [Fact]
    public void MatchingGenericMethodConstraintsCanBeInvoked()
    {
        var proxy = new MatchingConstraintTarget().DuckCast<IMatchingConstraintProxy>();

        proxy.Struct<int>().Should().Be(nameof(Int32));
        proxy.Reference<string>().Should().Be(nameof(String));
        proxy.Create<ConstraintImplementation>().Should().BeOfType<ConstraintImplementation>();
        proxy.Base<ConstraintImplementation>().Should().Be(nameof(ConstraintImplementation));
        proxy.Interface<ConstraintImplementation>().Should().Be(nameof(ConstraintImplementation));
        proxy.Multiple<ConstraintImplementation>().Should().Be(nameof(ConstraintImplementation));
        proxy.Related<ConstraintBase, ConstraintImplementation>().Should().BeTrue();
    }

    [Fact]
    public void GeneratedMethodPreservesConstraintMetadata()
    {
        var proxy = new MatchingConstraintTarget().DuckCast<IMatchingConstraintProxy>();
        var generatedType = proxy.GetType();

        AssertConstraintsMatch(
            typeof(IMatchingConstraintProxy).GetMethod(nameof(IMatchingConstraintProxy.Struct)),
            generatedType.GetMethod(nameof(IMatchingConstraintProxy.Struct)));
        AssertConstraintsMatch(
            typeof(IMatchingConstraintProxy).GetMethod(nameof(IMatchingConstraintProxy.Reference)),
            generatedType.GetMethod(nameof(IMatchingConstraintProxy.Reference)));
        AssertConstraintsMatch(
            typeof(IMatchingConstraintProxy).GetMethod(nameof(IMatchingConstraintProxy.Create)),
            generatedType.GetMethod(nameof(IMatchingConstraintProxy.Create)));
        AssertConstraintsMatch(
            typeof(IMatchingConstraintProxy).GetMethod(nameof(IMatchingConstraintProxy.Base)),
            generatedType.GetMethod(nameof(IMatchingConstraintProxy.Base)));
        AssertConstraintsMatch(
            typeof(IMatchingConstraintProxy).GetMethod(nameof(IMatchingConstraintProxy.Interface)),
            generatedType.GetMethod(nameof(IMatchingConstraintProxy.Interface)));
        AssertConstraintsMatch(
            typeof(IMatchingConstraintProxy).GetMethod(nameof(IMatchingConstraintProxy.Multiple)),
            generatedType.GetMethod(nameof(IMatchingConstraintProxy.Multiple)));

        var relatedArguments = generatedType
                              .GetMethod(nameof(IMatchingConstraintProxy.Related))
                              .GetGenericArguments();
        relatedArguments[1].GetGenericParameterConstraints().Should().ContainSingle()
                           .Which.Should().BeSameAs(relatedArguments[0]);
    }

    [Fact]
    public void ReverseProxyPreservesGenericMethodConstraints()
    {
        var implementation = new ReverseConstraintImplementation();
        var proxy = (IReverseConstraintContract)implementation.DuckImplement(typeof(IReverseConstraintContract));

        proxy.Struct<int>().Should().Be(nameof(Int32));

        var genericArgument = proxy.GetType()
                                   .GetMethod(nameof(IReverseConstraintContract.Struct))
                                   .GetGenericArguments()[0];
        genericArgument.GenericParameterAttributes.Should().Be(
            GenericParameterAttributes.NotNullableValueTypeConstraint |
            GenericParameterAttributes.DefaultConstructorConstraint);
    }

    [Fact]
    public void WeakerProxyConstraintsAreRejected()
    {
        var target = new ValueTypeConstraintTarget();

        DuckType.CanCreate<IUnconstrainedProxy>(target).Should().BeFalse();
        target.DuckIs<IUnconstrainedProxy>().Should().BeFalse();
    }

    private static void AssertConstraintsMatch(MethodInfo definitionMethod, MethodInfo generatedMethod)
    {
        var definitionArgument = definitionMethod.GetGenericArguments()[0];
        var generatedArgument = generatedMethod.GetGenericArguments()[0];

        generatedArgument.GenericParameterAttributes.Should().Be(definitionArgument.GenericParameterAttributes);
        generatedArgument.GetGenericParameterConstraints()
                         .Should()
                         .BeEquivalentTo(definitionArgument.GetGenericParameterConstraints());
    }

    private interface IMatchingConstraintProxy
    {
        string Struct<T>()
            where T : struct;

        string Reference<T>()
            where T : class;

        T Create<T>()
            where T : class, new();

        string Base<T>()
            where T : ConstraintBase;

        string Interface<T>()
            where T : IConstraintMarker;

        string Multiple<T>()
            where T : ConstraintBase, IConstraintMarker, new();

        bool Related<TBase, TDerived>()
            where TDerived : TBase;
    }

    private interface IReverseConstraintContract
    {
        string Struct<T>()
            where T : struct;
    }

    private interface IUnconstrainedProxy
    {
        string Describe<T>();
    }

    private class MatchingConstraintTarget
    {
        public string Struct<T>()
            where T : struct
            => typeof(T).Name;

        public string Reference<T>()
            where T : class
            => typeof(T).Name;

        public T Create<T>()
            where T : class, new()
            => new();

        public string Base<T>()
            where T : ConstraintBase
            => typeof(T).Name;

        public string Interface<T>()
            where T : IConstraintMarker
            => typeof(T).Name;

        public string Multiple<T>()
            where T : ConstraintBase, IConstraintMarker, new()
            => typeof(T).Name;

        public bool Related<TBase, TDerived>()
            where TDerived : TBase
            => typeof(TBase).IsAssignableFrom(typeof(TDerived));
    }

    private class ReverseConstraintImplementation
    {
        [DuckReverseMethod]
        public string Struct<T>()
            where T : struct
            => typeof(T).Name;
    }

    private class ValueTypeConstraintTarget
    {
        public string Describe<T>()
            where T : struct
            => typeof(T).Name;
    }

    private class ConstraintBase
    {
    }

    private interface IConstraintMarker
    {
    }

    private class ConstraintImplementation : ConstraintBase, IConstraintMarker
    {
    }
}
