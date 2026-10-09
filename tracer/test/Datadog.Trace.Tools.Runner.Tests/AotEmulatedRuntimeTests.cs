// <copyright file="AotEmulatedRuntimeTests.cs" company="Datadog">
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

public sealed unsafe class AotEmulatedRuntimeTests
{
    /// <summary>
    /// The native tracer requires ICorProfilerInfo12 on ARM (it means .NET 5+ there), and enables the runtime ReJIT path when
    /// it gets ICorProfilerInfo10: the emulated runtime exposes the first, through the native vtable, but not the second.
    /// </summary>
    [Fact]
    public void ExposesProfilerInfo12ButNotProfilerInfo10()
    {
        using var runtime = new EmulatedRuntime(new Version(8, 0, 0));
        var info = new NativeObjects.ICorProfilerInfo8Invoker(runtime.InfoPointer);

        info.QueryInterface(ICorProfilerInfo12.Guid, out var info12).Code.Should().Be(HResult.S_OK);
        info12.Should().NotBe(IntPtr.Zero);
        new NativeObjects.ICorProfilerInfo12Invoker(info12).GetRuntimeInformation(out _, out _, out var major, out _, out _, out _, 0, out _, null).Code.Should().Be(HResult.S_OK);
        major.Should().Be(8);

        info.QueryInterface(ICorProfilerInfo10.Guid, out var info10).Code.Should().Be(HResult.E_NOINTERFACE);
        info10.Should().Be(IntPtr.Zero);
    }
}
#endif
