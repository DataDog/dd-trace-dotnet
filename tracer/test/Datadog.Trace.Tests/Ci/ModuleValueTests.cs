// <copyright file="ModuleValueTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

// Context buffers come from a process-wide pool, so these tests run with the other tests that create them.
[Collection(nameof(CoverageGlobalStateTestCollection))]
public class ModuleValueTests
{
    private const int BufferSize = 24;

    [Fact]
    public void DisposedContextBufferIsReturnedToThePool()
    {
        var value = CreateModuleValue(ModuleValue.BufferKind.Context);
        var buffer = value.FilesLines;

        value.Dispose();

        value.FilesLines.Should().Be(IntPtr.Zero);
        value.AllocatedByteLength.Should().Be(0);
        ModuleValue.ContextBuffers.Contains(buffer).Should().BeTrue();
    }

    [Fact]
    public void FinalizedContextBufferIsReturnedToThePool()
    {
        var buffer = CreateAndAbandonContextBuffer();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        ModuleValue.ContextBuffers.Contains(buffer).Should().BeTrue();
    }

    [Fact]
    public void DisposedGlobalFallbackBufferIsNotPooled()
    {
        var value = CreateModuleValue(ModuleValue.BufferKind.GlobalFallback);
        var buffer = value.FilesLines;

        value.Dispose();

        value.FilesLines.Should().Be(IntPtr.Zero);
        ModuleValue.ContextBuffers.Contains(buffer).Should().BeFalse();
    }

    [Theory]
    [CombinatorialData]
    public void NewBuffersStartZeroedEvenWhenReused(bool contextBuffer)
    {
        var bufferKind = contextBuffer ? ModuleValue.BufferKind.Context : ModuleValue.BufferKind.GlobalFallback;
        var previous = CreateModuleValue(bufferKind);
        Marshal.Copy(Enumerable.Repeat((byte)0xFF, BufferSize).ToArray(), 0, previous.FilesLines, BufferSize);
        previous.Dispose();

        using var value = CreateModuleValue(bufferKind);

        var bytes = new byte[BufferSize];
        Marshal.Copy(value.FilesLines, bytes, 0, BufferSize);
        bytes.Should().OnlyContain(b => b == 0);
        value.AllocatedByteLength.Should().Be(BufferSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IntPtr CreateAndAbandonContextBuffer() => CreateModuleValue(ModuleValue.BufferKind.Context).FilesLines;

    private static ModuleValue CreateModuleValue(ModuleValue.BufferKind bufferKind)
        => new(
            new TestModuleCoverageMetadata(BufferSize, 0, [new FileCoverageMetadata("/src/module-value.cs", 0, BufferSize, new byte[3])]),
            typeof(ModuleValueTests).Module,
            BufferSize,
            bufferKind);
}
