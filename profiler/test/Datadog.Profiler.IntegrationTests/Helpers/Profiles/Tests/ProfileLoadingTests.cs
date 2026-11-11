// <copyright file="ProfileLoadingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class ProfileLoadingTests
    {
        [Fact]
        public void Read_LoadsRawLz4AndZstd_AndSumsLikeTheLegacyReader()
        {
            using var directory = new TempPprofDirectory();
            CpuProfile(10, 0).WriteRaw(directory.DirectoryPath, "raw.pprof");
            var lz4 = CpuProfile(20).WriteLz4(directory.DirectoryPath, "frame.pprof");
            var zstd = CpuProfile(5).WriteZstd(directory.DirectoryPath, Path.Combine("nested", "child.pprof"));
            File.WriteAllText(Path.Combine(directory.DirectoryPath, "notes.txt"), "not a profile");

            ReadHeader(lz4).Should().Equal(BitConverter.GetBytes(0x184D2204));
            ReadHeader(zstd).Should().Equal(BitConverter.GetBytes(0xFD2FB528));

            var output = ProfileReader.Read(directory.DirectoryPath);

            output.Samples.Count.Should().Be(SamplesHelper.GetSamplesCount(directory.DirectoryPath));
            output.Sum(ProfileValue.CpuTime).Should().Be(SamplesHelper.GetValueSum(directory.DirectoryPath, 0));
            output.Sum(ProfileValue.CpuTime).Should().Be(35);
            output.Sum(ProfileValue.CpuSampleCount).Should().Be(SamplesHelper.GetValueSum(directory.DirectoryPath, 1));
            output.Sum(ProfileValue.CpuSampleCount).Should().Be(4);
            output.Profiles.Should().HaveCount(3);
        }

        [Fact]
        public void Read_EmptyDirectory_HasNoSamples()
        {
            using var directory = new TempPprofDirectory();

            var output = ProfileReader.Read(directory.DirectoryPath);

            output.Profiles.Should().BeEmpty();
            output.Samples.Should().BeEmpty();
            output.Labels.Should().BeEmpty();
            output.CallStacks.Should().BeEmpty();
            output.Sum(ProfileValue.CpuTime).Should().Be(0);
            SamplesHelper.GetSamplesCount(directory.DirectoryPath).Should().Be(0);
            SamplesHelper.GetValueSum(directory.DirectoryPath, 0).Should().Be(0);
        }

        [Fact]
        public void ReadFile_KeepsPathAndTimestamp()
        {
            using var directory = new TempPprofDirectory();
            var path = CpuProfile(1).WriteRaw(directory.DirectoryPath, "profile.pprof");
            File.SetLastWriteTimeUtc(path, new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc));

            var profile = ProfileReader.ReadFile(path);

            profile.Path.Should().Be(path);
            profile.TimestampUtc.Should().Be(File.GetLastWriteTimeUtc(path));
        }

        [Fact]
        public void Read_ExposedCollections_CannotBeMutated()
        {
            using var directory = new TempPprofDirectory();
            CpuProfile(10).WriteRaw(directory.DirectoryPath, "profile.pprof");

            var output = ProfileReader.Read(directory.DirectoryPath);
            var profile = output.Profiles[0];

            var sampleTypes = (IList<ProfileValue>)profile.SampleTypes;
            sampleTypes.IsReadOnly.Should().BeTrue();
            Action replaceSampleType = () => sampleTypes[0] = ProfileValue.WallTime;
            replaceSampleType.Should().Throw<NotSupportedException>();

            var samples = (IList<ProfileSample>)profile.Samples;
            samples.IsReadOnly.Should().BeTrue();
            Action replaceSample = () => samples[0] = null;
            replaceSample.Should().Throw<NotSupportedException>();

            var profiles = (IList<PprofProfile>)output.Profiles;
            profiles.IsReadOnly.Should().BeTrue();
            Action replaceProfile = () => profiles[0] = null;
            replaceProfile.Should().Throw<NotSupportedException>();

            ((IList<ProfileLabels>)output.Labels).IsReadOnly.Should().BeTrue();
            ((IList<StackTrace>)output.CallStacks).IsReadOnly.Should().BeTrue();

            var read = profile.Samples[0].Values;
            read.TryGetValue(ProfileValue.WallTime).IsUnknown.Should().BeTrue();
            read.GetRequired(ProfileValue.CpuTime).Should().Be(10);
        }

        [Fact]
        public void ReadFile_TruncatedHeader_ThrowsAndReleasesTheFile()
        {
            using var directory = new TempPprofDirectory();
            var path = Path.Combine(directory.DirectoryPath, "truncated.pprof");
            File.WriteAllBytes(path, new byte[] { 1, 2 });

            var read = () => ProfileReader.ReadFile(path);

            read.Should().Throw<EndOfStreamException>();

            // Deleting the file fails on Windows when the read leaked its handle.
            File.Delete(path);
        }

        private static SyntheticProfile CpuProfile(params long[] cpu)
        {
            var profile = new SyntheticProfile()
                .SampleType("cpu", "nanoseconds")
                .SampleType("cpu-samples", "count");
            foreach (var value in cpu)
            {
                profile.AddSample(new[] { value, 1L }, System.Array.Empty<ulong>());
            }

            return profile;
        }

        private static byte[] ReadHeader(string path)
        {
            var buffer = new byte[4];
            using (var stream = File.OpenRead(path))
            {
                stream.Read(buffer, 0, buffer.Length).Should().Be(buffer.Length);
            }

            return buffer;
        }
    }
}
