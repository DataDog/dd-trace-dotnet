// <copyright file="OptionalParameterMethodTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Methods;

#pragma warning disable SA1201 // Elements should appear in the correct order

public class OptionalParameterMethodTests
{
    public enum OptionalParameterKind
    {
        Int32,
        String,
        Enum,
        NullableInt32,
        MultipleParameters,
    }

    [Theory]
    [InlineData(OptionalParameterKind.Int32)]
    [InlineData(OptionalParameterKind.String)]
    [InlineData(OptionalParameterKind.Enum)]
    [InlineData(OptionalParameterKind.NullableInt32)]
    [InlineData(OptionalParameterKind.MultipleParameters)]
    public void OmittedOptionalTargetParameterIsRejected(OptionalParameterKind parameterKind)
    {
        var target = CreateTarget(parameterKind);

        DuckType.CanCreate<IParameterlessProxy>(target).Should().BeFalse();
        target.DuckIs<IParameterlessProxy>().Should().BeFalse();
        target.TryDuckCast<IParameterlessProxy>(out var proxy).Should().BeFalse();
        proxy.Should().BeNull();
    }

    [Fact]
    public void OptionalTargetParameterCanBeSuppliedByProxy()
    {
        var target = new Int32OptionalParameterTarget();
        var proxy = target.DuckCast<IExplicitParameterProxy>();

        proxy.GetValue(21).Should().Be(21);
    }

    private static object CreateTarget(OptionalParameterKind parameterKind)
        => parameterKind switch
        {
            OptionalParameterKind.Int32 => new Int32OptionalParameterTarget(),
            OptionalParameterKind.String => new StringOptionalParameterTarget(),
            OptionalParameterKind.Enum => new EnumOptionalParameterTarget(),
            OptionalParameterKind.NullableInt32 => new NullableOptionalParameterTarget(),
            OptionalParameterKind.MultipleParameters => new MultipleOptionalParametersTarget(),
            _ => throw new ArgumentOutOfRangeException(nameof(parameterKind), parameterKind, null),
        };

    private interface IParameterlessProxy
    {
        int GetValue();
    }

    private interface IExplicitParameterProxy
    {
        int GetValue(int value);
    }

    private class Int32OptionalParameterTarget
    {
        public int GetValue(int value = 42) => value;
    }

    private class StringOptionalParameterTarget
    {
        public int GetValue(string value = "expected") => value.Length;
    }

    private class EnumOptionalParameterTarget
    {
        public int GetValue(OptionalValue value = OptionalValue.Second) => (int)value;
    }

    private class NullableOptionalParameterTarget
    {
        public int GetValue(int? value = null) => value ?? -1;
    }

    private class MultipleOptionalParametersTarget
    {
        public int GetValue(int first = 21, int second = 21) => first + second;
    }

    private enum OptionalValue
    {
        First,
        Second,
    }
}
