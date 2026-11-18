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
        ModuleValue.ByteCounterContextBuffers.Contains(buffer).Should().BeTrue();
    }

    [Fact]
    public void FinalizedContextBufferIsReturnedToThePool()
    {
        var buffer = CreateAndAbandonContextBuffer();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        ModuleValue.ByteCounterContextBuffers.Contains(buffer).Should().BeTrue();
    }

    [Fact]
    public void DisposedGlobalFallbackBufferIsNotPooled()
    {
        var value = CreateModuleValue(ModuleValue.BufferKind.GlobalFallback);
        var buffer = value.FilesLines;

        value.Dispose();

        value.FilesLines.Should().Be(IntPtr.Zero);
        ModuleValue.ByteCounterContextBuffers.Contains(buffer).Should().BeFalse();
    }

    [Fact]
    public void ContextBuffersAreNotReusedAcrossCounterWidths()
    {
        var lineCallCount = CreateModuleValue(ModuleValue.BufferKind.Context, coverageMode: 1);
        var released = lineCallCount.FilesLines;
        lineCallCount.Dispose();

        using var lineExecution = CreateModuleValue(ModuleValue.BufferKind.Context, coverageMode: 0);

        lineExecution.FilesLines.Should().NotBe(released);
        ModuleValue.IntCounterContextBuffers.Contains(released).Should().BeTrue();
        ModuleValue.ByteCounterContextBuffers.Contains(released).Should().BeFalse();
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

    // Both modes use the same raw buffer size here, so only the counter width tells the buffers apart.
    private static ModuleValue CreateModuleValue(ModuleValue.BufferKind bufferKind, int coverageMode = 0)
    {
        var lines = coverageMode == 0 ? BufferSize : BufferSize / sizeof(int);
        return new(
            new TestModuleCoverageMetadata(lines, coverageMode, [new FileCoverageMetadata("/src/module-value.cs", 0, lines, new byte[(lines + 7) / 8])]),
            typeof(ModuleValueTests).Module,
            BufferSize,
            bufferKind);
    }
}
