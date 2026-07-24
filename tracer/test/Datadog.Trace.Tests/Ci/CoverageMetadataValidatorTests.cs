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
        => Validate(new TestModuleCoverageMetadata(1, 2, [new FileCoverageMetadata("file", 0, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsNegativeTotalLines()
        => Validate(new TestModuleCoverageMetadata(-1, 0, [])).Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsRawCounterSizeOverflow()
        => Validate(new TestModuleCoverageMetadata(int.MaxValue, 1, [])).Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsNegativeFileRange()
        => Validate(new TestModuleCoverageMetadata(1, 0, [new FileCoverageMetadata("file", -1, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsOverflowingFileRange()
        => Validate(new TestModuleCoverageMetadata(int.MaxValue, 0, [new FileCoverageMetadata("file", int.MaxValue, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsFileRangeOutsideRawBuffer()
        => Validate(new TestModuleCoverageMetadata(1, 0, [new FileCoverageMetadata("file", 1, 1, [0x80])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public void RejectsExecutableBitmapWithWrongSize()
        => Validate(new TestModuleCoverageMetadata(9, 0, [new FileCoverageMetadata("file", 0, 9, [0xff])]))
          .Should().Throw<InvalidOperationException>();

    [Fact]
    public unsafe void InvalidReporterMetadataSuppressesGlobalOutput()
    {
        var previousHandler = CoverageReporter.Handler;
        var handler = new DefaultWithGlobalCoverageEventHandler();
        CoverageReporter.Handler = handler;
        try
        {
            var action = () => Probe<InvalidReporterMetadata>();

            action.Should().Throw<TypeInitializationException>();
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

    private sealed class InvalidReporterMetadata : TestModuleCoverageMetadata
    {
        public InvalidReporterMetadata()
            : base(int.MaxValue, 1, [])
        {
        }
    }
}
