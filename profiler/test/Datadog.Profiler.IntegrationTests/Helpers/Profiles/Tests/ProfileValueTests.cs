// <copyright file="ProfileValueTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class ProfileValueTests
    {
        [Fact]
        public void TypedKeys_MatchCurrentSampleTypeNamesAndUnits()
        {
            ProfileValue.CpuTime.Should().Be(ProfileValue.FromSampleType("cpu", "nanoseconds"));
            ProfileValue.CpuSampleCount.Should().Be(ProfileValue.FromSampleType("cpu-samples", "count"));
            ProfileValue.WallTime.Should().Be(ProfileValue.FromSampleType("wall", "nanoseconds"));
            ProfileValue.AllocationCount.Should().Be(ProfileValue.FromSampleType("alloc-samples", "count"));
            ProfileValue.AllocationSize.Should().Be(ProfileValue.FromSampleType("alloc-size", "bytes"));
            ProfileValue.ExceptionCount.Should().Be(ProfileValue.FromSampleType("exception", "count"));
            ProfileValue.LockCount.Should().Be(ProfileValue.FromSampleType("lock-count", "count"));
            ProfileValue.LockTime.Should().Be(ProfileValue.FromSampleType("lock-time", "nanoseconds"));
            ProfileValue.Timeline.Should().Be(ProfileValue.FromSampleType("timeline", "nanoseconds"));
            ProfileValue.RequestTime.Should().Be(ProfileValue.FromSampleType("request-time", "nanoseconds"));
            ProfileValue.LiveObjectCount.Should().Be(ProfileValue.FromSampleType("inuse-objects", "count"));
            ProfileValue.LiveObjectSpace.Should().Be(ProfileValue.FromSampleType("inuse-space", "bytes"));
        }

        [Fact]
        public void Read_ExposesEveryCurrentSampleType()
        {
            var expected = new[]
            {
                ProfileValue.CpuTime,
                ProfileValue.CpuSampleCount,
                ProfileValue.WallTime,
                ProfileValue.AllocationCount,
                ProfileValue.AllocationSize,
                ProfileValue.ExceptionCount,
                ProfileValue.LockCount,
                ProfileValue.LockTime,
                ProfileValue.Timeline,
                ProfileValue.RequestTime,
                ProfileValue.LiveObjectCount,
                ProfileValue.LiveObjectSpace,
            };
            var values = new long[expected.Length];
            var profile = new SyntheticProfile();
            for (var i = 0; i < expected.Length; i++)
            {
                values[i] = i + 1;
                profile.SampleType(expected[i].Name, expected[i].Unit);
            }

            profile.AddSample(values, Array.Empty<ulong>());
            var read = Load(profile);

            read.Should().HaveSampleTypes(expected);
            var sample = read.Samples[0];
            for (var i = 0; i < expected.Length; i++)
            {
                sample.Values.GetRequired(expected[i]).Should().Be(i + 1);
                var legacyName = read.SampleTypes[i].Name;
                legacyName.Should().Be(expected[i].Name);
                read.SampleTypes[i].Unit.Should().Be(expected[i].Unit);
            }
        }

        [Fact]
        public void Named_ReadsASampleTypeThatHasNoTypedKey()
        {
            var profile = new SyntheticProfile()
                .SampleType("custom-counter", "widgets")
                .AddSample(new long[] { 9 }, Array.Empty<ulong>());
            var sample = Load(profile).Samples[0];

            sample.Values.TryGetValue(ProfileValue.Named("custom-counter")).IsPresent.Should().BeTrue();
            sample.Values.GetRequired(ProfileValue.Named("custom-counter")).Should().Be(9);
            sample.Values.TryGetValue(ProfileValue.CpuTime).IsUnknown.Should().BeTrue();
        }

        [Fact]
        public void Lookup_IsByName_WhenTheUnitDiffersFromTheTypedKey()
        {
            var profile = new SyntheticProfile()
                .SampleType("cpu", "cycles")
                .AddSample(new long[] { 4 }, Array.Empty<ulong>());
            var read = Load(profile);

            read.SampleTypes[0].Unit.Should().Be("cycles");
            read.Samples[0].Values.GetRequired(ProfileValue.CpuTime).Should().Be(4);
            read.Samples[0].Values.TryGetValue(ProfileValue.CpuTime).IsUnknown.Should().BeFalse();
        }

        [Fact]
        public void UnknownMissingAndZero_StayDistinct()
        {
            using var directory = new TempPprofDirectory();
            var path = new SyntheticProfile()
                .SampleType("cpu", "nanoseconds")
                .SampleType("cpu-samples", "count")
                .AddSample(new long[] { 0 }, Array.Empty<ulong>())
                .WriteRaw(directory.DirectoryPath, "profile.pprof");
            var read = ProfileReader.ReadFile(path);
            var values = read.Samples[0].Values;

            var zero = values.TryGetValue(ProfileValue.CpuTime);
            zero.IsPresent.Should().BeTrue();
            zero.IsZero.Should().BeTrue();
            zero.IsMissing.Should().BeFalse();
            zero.IsUnknown.Should().BeFalse();
            values.GetRequired(ProfileValue.CpuTime).Should().Be(0);

            var missing = values.TryGetValue(ProfileValue.CpuSampleCount);
            missing.IsMissing.Should().BeTrue();
            missing.IsPresent.Should().BeFalse();
            missing.IsZero.Should().BeFalse();
            missing.IsUnknown.Should().BeFalse();
            var missingError = Assert.Throws<InvalidOperationException>(() => values.GetRequired(ProfileValue.CpuSampleCount));
            missingError.Message.Should().Contain("missing");
            missingError.Message.Should().Contain(path);
            missingError.Message.Should().Contain(read.TimestampUtc.ToString("o"));
            missingError.Message.Should().NotContain("does not contain");

            var unknown = values.TryGetValue(ProfileValue.WallTime);
            unknown.IsUnknown.Should().BeTrue();
            unknown.IsMissing.Should().BeFalse();
            unknown.IsPresent.Should().BeFalse();
            unknown.IsZero.Should().BeFalse();
            var unknownError = Assert.Throws<InvalidOperationException>(() => values.GetRequired(ProfileValue.WallTime));
            unknownError.Message.Should().Contain("does not contain");
            unknownError.Message.Should().Contain("wall");
            unknownError.Message.Should().Contain(path);
            unknownError.Message.Should().Contain("cpu (nanoseconds)");
            unknownError.Message.Should().Contain("cpu-samples (count)");
            unknownError.Message.Should().NotContain("missing");
        }

        private static PprofProfile Load(SyntheticProfile profile)
        {
            using var directory = new TempPprofDirectory();
            var path = profile.WriteRaw(directory.DirectoryPath, "profile.pprof");
            return ProfileReader.ReadFile(path);
        }
    }
}
