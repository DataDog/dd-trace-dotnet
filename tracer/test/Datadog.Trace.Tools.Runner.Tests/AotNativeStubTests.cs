// <copyright file="AotNativeStubTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using Datadog.Trace.Tools.Runner.Aot.Native;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The native objects the stub generator creates for the <c>[NativeObject]</c> interfaces (the CLR profiling and metadata
/// APIs the native tracer calls): native code calls the managed implementation through their vtable, and an exception of
/// the implementation is reported instead of crossing into native code.
/// </summary>
public class AotNativeStubTests
{
    [Fact]
    public void NativeCallsReachTheManagedImplementation()
    {
        var implementation = new CountingUnknown();
        using var stub = NativeObjects.IUnknown.Wrap(implementation);
        var invoker = new NativeObjects.IUnknownInvoker(stub);

        var guid = Guid.NewGuid();
        invoker.QueryInterface(in guid, out var pointer).Code.Should().Be(HResult.S_OK);
        ((long)pointer).Should().Be(42);
        implementation.LastGuid.Should().Be(guid);
        invoker.AddRef().Should().Be(1);
        invoker.AddRef().Should().Be(2);
        invoker.Release().Should().Be(1);
    }

    [Fact]
    public void ExceptionsOfTheImplementationAreReturnedAsFailures()
    {
        using var stub = NativeObjects.IUnknown.Wrap(new ThrowingUnknown());
        var invoker = new NativeObjects.IUnknownInvoker(stub);

        var guid = Guid.NewGuid();
        invoker.QueryInterface(in guid, out _).Code.Should().Be(HResult.E_FAIL);
        NativeStubDiagnostics.Errors.Should().Contain(error => error.Contains("IUnknown.QueryInterface") && error.Contains(guid.ToString()));
    }

    private sealed class CountingUnknown : IUnknown
    {
        private int _references;

        public Guid LastGuid { get; private set; }

        public HResult QueryInterface(in Guid guid, out IntPtr ptr)
        {
            LastGuid = guid;
            ptr = new IntPtr(42);
            return HResult.S_OK;
        }

        public int AddRef() => ++_references;

        public int Release() => --_references;
    }

    private sealed class ThrowingUnknown : IUnknown
    {
        public HResult QueryInterface(in Guid guid, out IntPtr ptr) => throw new InvalidOperationException($"No interface {guid}");

        public int AddRef() => 1;

        public int Release() => 0;
    }
}
#endif
