// <copyright file="CoverageMetadataValidatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class CoverageMetadataValidatorTests
{
    [Fact]
    public void RejectsUnsupportedCoverageMode()
        => Validate(new TestMetadata(1, 2, [new FileCoverageMetadata("file", 0, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsNegativeTotalLines()
        => Validate(new TestMetadata(-1, 0, [])).Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsRawCounterSizeOverflow()
        => Validate(new TestMetadata(int.MaxValue, 1, [])).Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsNegativeFileRange()
        => Validate(new TestMetadata(1, 0, [new FileCoverageMetadata("file", -1, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsOverflowingFileRange()
        => Validate(new TestMetadata(int.MaxValue, 0, [new FileCoverageMetadata("file", int.MaxValue, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsFileRangeOutsideRawBuffer()
        => Validate(new TestMetadata(1, 0, [new FileCoverageMetadata("file", 1, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsExecutableBitmapWithWrongSize()
        => Validate(new TestMetadata(9, 0, [new FileCoverageMetadata("file", 0, 9, [0xff])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public unsafe void InvalidReporterMetadataAllocatesNoNativeMemoryAndSuppressesGlobalOutput()
    {
        var previousHandler = CoverageReporter.Handler;
        var diagnostics = new CoverageNativeAllocationDiagnostics();
        var strategy = new CountingStrategy(diagnostics);
        var handler = new DefaultWithGlobalCoverageEventHandler(moduleValueStrategy: strategy);
        CoverageReporter.Handler = handler;
        try
        {
            var action = () => Probe<InvalidReporterMetadata>();

            action.Should().Throw<TypeInitializationException>();
            strategy.AllocateCalls.Should().Be(0);
            strategy.FreeCalls.Should().Be(0);
            diagnostics.GetSnapshot(CoverageModuleValueOrigin.TestContext).ActiveBuffers.Should().Be(0);
            handler.AccumulatorDiagnostics.IsValid.Should().BeFalse();
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
        }
    }

    private static Action Validate(ModuleCoverageMetadata metadata)
        => () => CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata);

    private static unsafe void Probe<TMetadata>()
        where TMetadata : ModuleCoverageMetadata, new()
        => _ = CoverageReporter<TMetadata>.GetFileCounter(0);

    private sealed class TestMetadata : ModuleCoverageMetadata
    {
        internal TestMetadata(int totalLines, int coverageMode, FileCoverageMetadata[] files)
            : base(totalLines, coverageMode, files)
        {
        }
    }

    private sealed class InvalidReporterMetadata : ModuleCoverageMetadata
    {
        public InvalidReporterMetadata()
            : base(int.MaxValue, 1, [])
        {
        }
    }

    private sealed class CountingStrategy : CoverageModuleValueStrategy
    {
        internal CountingStrategy(CoverageNativeAllocationDiagnostics diagnostics)
            : base(diagnostics)
        {
        }

        internal int AllocateCalls { get; private set; }

        internal int FreeCalls { get; private set; }

        internal override IntPtr Allocate(int byteLength, CoverageModuleValueOrigin origin)
        {
            AllocateCalls++;
            return base.Allocate(byteLength, origin);
        }

        internal override void Free(IntPtr pointer, CoverageModuleValueOrigin origin)
        {
            FreeCalls++;
            base.Free(pointer, origin);
        }
    }
}
