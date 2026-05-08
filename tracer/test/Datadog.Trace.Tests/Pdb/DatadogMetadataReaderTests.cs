// <copyright file="DatadogMetadataReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Pdb;
using FluentAssertions;
using Xunit;

#if NETCOREAPP3_1_OR_GREATER
using System.Reflection.Metadata;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
#endif

namespace Datadog.Trace.Tests.Pdb;

public class DatadogMetadataReaderTests
{
    [Fact]
    public unsafe void TryReadLocalVariablesCount_ValidSignature_ReturnsCount()
    {
        byte[] signature = [(byte)SignatureKind.LocalVariables, 1, 0x08];

        fixed (byte* ptr = signature)
        {
            var reader = new BlobReader(ptr, signature.Length);

            DatadogMetadataReader.TryReadLocalVariablesCount(ref reader, out var count).Should().BeTrue();
            count.Should().Be(1);
        }
    }

    [Fact]
    public unsafe void TryReadLocalVariablesCount_TruncatedSignature_ReturnsFalse()
    {
        byte[] signature = [(byte)SignatureKind.LocalVariables];

        fixed (byte* ptr = signature)
        {
            var reader = new BlobReader(ptr, signature.Length);

            DatadogMetadataReader.TryReadLocalVariablesCount(ref reader, out var count).Should().BeFalse();
            count.Should().Be(0);
        }
    }

    [Fact]
    public unsafe void TryDecodeLocalSignature_ValidSignature_ReturnsLocalTypes()
    {
        byte[] signature = [(byte)SignatureKind.LocalVariables, 1, 0x08];

        fixed (byte* ptr = signature)
        {
            var reader = new BlobReader(ptr, signature.Length);
            using var metadataReader = DatadogMetadataReader.CreatePdbReader(typeof(DatadogMetadataReaderTests).Assembly);

            if (metadataReader is null)
            {
                throw new InvalidOperationException("Could not create metadata reader for test assembly.");
            }

            DatadogMetadataReader.TryDecodeLocalSignature(metadataReader.MetadataReader, ref reader, out var localTypes).Should().BeTrue();
            localTypes.Should().Equal("System.Int32");
        }
    }

    [Fact]
    public unsafe void TryDecodeLocalSignature_TruncatedAfterCount_ReturnsFalse()
    {
        byte[] signature = [(byte)SignatureKind.LocalVariables, 1];

        fixed (byte* ptr = signature)
        {
            var reader = new BlobReader(ptr, signature.Length);
            using var metadataReader = DatadogMetadataReader.CreatePdbReader(typeof(DatadogMetadataReaderTests).Assembly);

            if (metadataReader is null)
            {
                throw new InvalidOperationException("Could not create metadata reader for test assembly.");
            }

            DatadogMetadataReader.TryDecodeLocalSignature(metadataReader.MetadataReader, ref reader, out var localTypes).Should().BeFalse();
            localTypes.IsDefault.Should().BeTrue();
        }
    }
}
