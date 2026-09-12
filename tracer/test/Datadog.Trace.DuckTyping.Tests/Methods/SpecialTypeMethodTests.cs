// <copyright file="SpecialTypeMethodTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.DuckTyping.Tests.Methods;

public unsafe class SpecialTypeMethodTests
{
    [Fact]
    public void ExactManagedReferenceReturnCanBeInvoked()
    {
        var target = new ManagedReferenceTarget();
        var proxy = target.DuckCast<IExactManagedReferenceProxy>();

        ref var value = ref proxy.GetReference();
        value = 42;

        target.Value.Should().Be(42);
    }

    [Theory]
    [InlineData(typeof(IIncompatibleManagedReferenceProxy))]
    [InlineData(typeof(IManagedReferenceAsObjectProxy))]
    public void IncompatibleManagedReferenceReturnIsRejected(Type proxyType)
    {
        var target = new ManagedReferenceTarget();

        DuckType.CanCreate(proxyType, target).Should().BeFalse();
        target.DuckIs(proxyType).Should().BeFalse();
    }

    [Fact]
    public void ExactPointerReturnCanBeInvoked()
    {
        var proxy = new PointerTarget().DuckCast<IExactPointerProxy>();

        ((nint)proxy.GetPointer()).Should().Be(new nint(0x1234));
    }

    [Theory]
    [InlineData(typeof(IIncompatiblePointerProxy))]
    [InlineData(typeof(IPointerAsObjectProxy))]
    public void IncompatiblePointerReturnIsRejected(Type proxyType)
    {
        var target = new PointerTarget();

        DuckType.CanCreate(proxyType, target).Should().BeFalse();
        target.DuckIs(proxyType).Should().BeFalse();
    }

#if NET5_0_OR_GREATER
    [Fact]
    public void FunctionPointerReturnedAsObjectIsRejected()
    {
        var target = new FunctionPointerTarget();

        DuckType.CanCreate<IFunctionPointerAsObjectProxy>(target).Should().BeFalse();
        target.DuckIs<IFunctionPointerAsObjectProxy>().Should().BeFalse();
    }
#endif

    private interface IExactManagedReferenceProxy
    {
        ref int GetReference();
    }

    private interface IIncompatibleManagedReferenceProxy
    {
        ref long GetReference();
    }

    private interface IManagedReferenceAsObjectProxy
    {
        object GetReference();
    }

    private interface IExactPointerProxy
    {
        long* GetPointer();
    }

    private interface IIncompatiblePointerProxy
    {
        int* GetPointer();
    }

    private interface IPointerAsObjectProxy
    {
        object GetPointer();
    }

#if NET5_0_OR_GREATER
    private interface IFunctionPointerAsObjectProxy
    {
        object GetFunctionPointer();
    }
#endif

    private class ManagedReferenceTarget
    {
        private int _value = 21;

        public int Value => _value;

        public ref int GetReference() => ref _value;
    }

    private class PointerTarget
    {
        public long* GetPointer() => (long*)0x1234;
    }

#if NET5_0_OR_GREATER
    private class FunctionPointerTarget
    {
        public delegate*<void> GetFunctionPointer() => &Empty;

        private static void Empty()
        {
        }
    }
#endif
}
