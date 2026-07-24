// <copyright file="GlobalCoverageConsumerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class GlobalCoverageConsumerTests
{
    [Fact]
    public void InputReaderAcceptsBoundedCoverageWithUtf8Bom()
    {
        var path = Path.GetTempFileName();
        try
        {
            var model = CreateModel("component", "/src/example.cs", [0xff], [0x80]);
            using (var stream = File.Create(path))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                JsonSerializer.Create().Serialize(writer, model);
            }

            var reader = new GlobalCoverageInputReader();
            reader.TryRead(path, out var result).Should().BeTrue();
            var file = result!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
            file.Path.Should().Be("/src/example.cs");
            file.ExecutedBitmap.Should().Equal(0x80);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PreflightRejectsBitmapBeforeGeneralDeserializationCanMaterializeIt()
    {
        var limits = CreateSmallLimits(maximumBitmapBytes: 2, maximumIdentityCharacters: 128);
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                "{\"components\":[{\"name\":\"c\",\"files\":[{\"path\":\"p\",\"executableBitmap\":\"AQID\",\"executedBitmap\":\"AQ==\"}]}]}",
                new UTF8Encoding(false));

            var reader = new GlobalCoverageInputReader(limits);
            reader.TryRead(path, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PreflightRejectsIdentityCharacterBudget()
    {
        var limits = CreateSmallLimits(maximumBitmapBytes: 8, maximumIdentityCharacters: 3);
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                "{\"components\":[{\"name\":\"name\",\"files\":[]}]}",
                new UTF8Encoding(false));

            var reader = new GlobalCoverageInputReader(limits);
            reader.TryRead(path, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{\"components\":[null]}")]
    [InlineData("{\"components\":[{\"name\":\"component\",\"files\":[null]}]}")]
    public void InputReaderRejectsNullCoverageEntries(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json, new UTF8Encoding(false));

            var reader = new GlobalCoverageInputReader();
            reader.TryRead(path, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{\"components\":[{\"name\":\"c\",\"files\":[{\"path\":\"p\",\"executableBitmap\":\"gA==\",\"executedBitmap\":\"/w==\"}]}]}")]
    [InlineData("{\"components\":[{\"name\":\"c\",\"files\":[{\"path\":\"p\",\"executedBitmap\":\"gA==\"}]}]}")]
    [InlineData("{\"components\":[{\"name\":\"c\",\"files\":[{\"path\":\"p\",\"executableBitmap\":\"gA==\",\"executedBitmap\":\"gAA=\"}]}]}")]
    public void InputReaderRejectsExecutedBitmapThatIsNotACompatibleExecutableSubset(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json, new UTF8Encoding(false));

            var reader = new GlobalCoverageInputReader();
            reader.TryRead(path, out _).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InputReaderAcceptsShorterExecutedBitmapAsImplicitTrailingZeros()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                "{\"components\":[{\"name\":\"c\",\"files\":[{\"path\":\"p\",\"executableBitmap\":\"//8=\",\"executedBitmap\":\"gA==\"}]}]}",
                new UTF8Encoding(false));

            var reader = new GlobalCoverageInputReader();
            reader.TryRead(path, out var result).Should().BeTrue();
            result!.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject.Data.Should().Equal(6.25, 16, 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CombinerUnionsInputsIncrementallyWithoutRetainingInputModels()
    {
        var accumulator = new GlobalCoverageCombinerAccumulator(CreateSmallLimits(maximumBitmapBytes: 8, maximumIdentityCharacters: 128));
        accumulator.Add(CreateModel(null, null, [0xf0], [0x80]));
        accumulator.Add(CreateModel(null, null, [0x0f, 0xff], [0x08, 0x80]));

        var model = accumulator.Materialize();
        var file = model.Components.Should().ContainSingle().Subject.Files.Should().ContainSingle().Subject;
        file.ExecutableBitmap.Should().Equal(0xff, 0xff);
        file.ExecutedBitmap.Should().Equal(0x88, 0x80);
        file.Data.Should().Equal(18.75, 16, 3);
    }

    [Fact]
    public void ArtifactWriterPublishesUtf8WithoutBomAndReaderAcceptsIt()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(directory, "coverage.json");
        try
        {
            var writer = new GlobalCoverageArtifactWriter();
            writer.WriteAtomicNoReplace(path, CreateModel("component", "/src/example.cs", [0xff], [0x80]));

            var bytes = File.ReadAllBytes(path);
            (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf).Should().BeFalse();
            var reader = new GlobalCoverageInputReader();
            reader.TryRead(path, out _).Should().BeTrue();
            Directory.GetFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ArtifactWriterLimitFailurePreservesDestinationAndCleansTemporary()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(directory, "coverage.json");
        var original = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(path, original);
        try
        {
            var limits = CreateSmallLimits(maximumBitmapBytes: 8, maximumIdentityCharacters: 128, maximumSerializedBytes: 1);
            var writer = new GlobalCoverageArtifactWriter(limits);
            var action = () => writer.WriteAtomicReplace(path, CreateModel("component", "/src/example.cs", [0xff], [0x80]));

            action.Should().Throw<InvalidDataException>();
            File.ReadAllBytes(path).Should().Equal(original);
            Directory.GetFiles(directory).Should().ContainSingle().Which.Should().Be(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ArtifactWriterNoReplaceNeverOverwritesExistingArtifact()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(directory, "coverage.json");
        var original = new byte[] { 5, 6, 7, 8 };
        File.WriteAllBytes(path, original);
        try
        {
            var writer = new GlobalCoverageArtifactWriter();
            var action = () => writer.WriteAtomicNoReplace(path, CreateModel("component", "/src/example.cs", [0xff], [0x80]));

            action.Should().Throw<IOException>();
            File.ReadAllBytes(path).Should().Equal(original);
            Directory.GetFiles(directory).Should().ContainSingle().Which.Should().Be(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void StagedArtifactDoesNotPublishUntilCommitAndDisposeCleansUncommittedTemporary()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(directory, "coverage.json");
        try
        {
            var writer = new GlobalCoverageArtifactWriter();
            using (var staged = writer.StageNoReplace(path, CreateModel("component", "/src/example.cs", [0xff], [0x80])))
            {
                File.Exists(path).Should().BeFalse();
                Directory.GetFiles(directory).Should().ContainSingle().Which.Should().EndWith(".tmp");
            }

            Directory.GetFiles(directory).Should().BeEmpty();
            using (var staged = writer.StageNoReplace(path, CreateModel("component", "/src/example.cs", [0xff], [0x80])))
            {
                staged.Commit();
            }

            File.Exists(path).Should().BeTrue();
            Directory.GetFiles(directory).Should().ContainSingle().Which.Should().Be(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static GlobalCoverageInfo CreateModel(string? componentName, string? path, byte[] executable, byte[] executed)
    {
        var model = new GlobalCoverageInfo();
        var component = new ComponentCoverageInfo(componentName);
        component.Files.Add(new FileCoverageInfo(path) { ExecutableBitmap = executable, ExecutedBitmap = executed });
        model.Components.Add(component);
        return model;
    }

    private static GlobalCoverageArtifactLimits CreateSmallLimits(int maximumBitmapBytes, int maximumIdentityCharacters, long maximumSerializedBytes = 4 * 1024)
        => new(
            maximumSerializedBytes: maximumSerializedBytes,
            maximumBitmapBytes: maximumBitmapBytes,
            maximumModelBitmapBytes: 64,
            maximumComponents: 4,
            maximumEntries: 8,
            maximumIdentityCharacters: maximumIdentityCharacters,
            maximumPropertyCharacters: 64,
            maximumScalarCharacters: 256,
            maximumDepth: 16,
            scannerBufferCharacters: 128);
}
