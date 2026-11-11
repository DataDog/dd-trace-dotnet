// <copyright file="ProfileQueryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class ProfileQueryTests
    {
        [Fact]
        public void Samples_FilterByNonZeroValueAndLabel_AndFlattenLabelsAndStacks()
        {
            using var directory = new TempPprofDirectory();
            var profile = new SyntheticProfile()
                .SampleType("cpu", "nanoseconds")
                .SampleType("cpu-samples", "count");
            var main = profile.Function("Main", "Main.cs");
            var worker = profile.Function("Worker", "Worker.cs");
            var mainLocation = profile.Location(main, 1);
            var workerLocation = profile.Location(worker, 2);
            profile.AddSample(
                new long[] { 0, 1 },
                new[] { mainLocation },
                SyntheticProfile.SampleLabel.String("thread name", "main"));
            profile.AddSample(
                new long[] { 8, 1 },
                new[] { workerLocation },
                SyntheticProfile.SampleLabel.String("thread name", "worker"));
            profile.WriteRaw(directory.DirectoryPath, "profile.pprof");

            var output = ProfileReader.Read(directory.DirectoryPath);

            output.Sum(ProfileValue.CpuTime).Should().Be(8);
            output.WithNonZeroValue(ProfileValue.CpuTime).Should().ContainSingle()
                .Which.Values.GetRequired(ProfileValue.CpuTime).Should().Be(8);
            output.Samples.WithLabel("thread name", "main").Should().ContainSingle()
                .Which.Values.GetRequired(ProfileValue.CpuTime).Should().Be(0);
            output.Labels.Should().HaveCount(2);
            output.CallStacks.Should().HaveCount(2);
            output.CallStacks[0][0].Function.Should().Be("Main");
            output.Samples.WithLabel("thread name", "worker").CallStacks[0][0].Function.Should().Be("Worker");

            output.Profiles[0].Should().HaveSampleTypes(ProfileValue.CpuTime, ProfileValue.CpuSampleCount);
            var path = output.Profiles[0].Path;
            Action wrongTypes = () => output.Profiles[0].Should().HaveSampleTypes(ProfileValue.WallTime);
            wrongTypes.Should().Throw<Exception>()
                .Which.Message.Should().Contain("wall (nanoseconds)")
                .And.Contain("cpu (nanoseconds)")
                .And.Contain(path);
        }

        [Fact]
        public void Output_LabelHelpers_AndSampleTypesOfEveryProfile()
        {
            using var directory = new TempPprofDirectory();
            var first = new SyntheticProfile().SampleType("wall", "nanoseconds");
            var location = first.Location(first.Function("Main", "Main.cs"), 1);
            first.AddSample(new long[] { 1 }, new[] { location }, SyntheticProfile.SampleLabel.String("thread id", "1"));
            first.AddSample(new long[] { 2 }, new[] { location }, SyntheticProfile.SampleLabel.String("thread id", "2"));
            first.WriteRaw(directory.DirectoryPath, "first.pprof");
            var second = new SyntheticProfile().SampleType("wall", "nanoseconds");
            second.AddSample(new long[] { 3 }, new[] { second.Location(second.Function("Main", "Main.cs"), 1) }, SyntheticProfile.SampleLabel.String("thread id", "1"));
            second.WriteRaw(directory.DirectoryPath, "second.pprof");

            var output = ProfileReader.Read(directory.DirectoryPath);

            output.Should().HaveSampleTypes(ProfileValue.WallTime);
            Action wrongTypes = () => output.Should().HaveSampleTypes(ProfileValue.CpuTime);
            wrongTypes.Should().Throw<Exception>();

            output.Samples.DistinctLabelValues("thread id").Should().BeEquivalentTo("1", "2");
            output.Samples[0].Labels.Has("thread id").Should().BeTrue();
            output.Samples[0].Labels.Has("thread name").Should().BeFalse();

            output.EveryProfileHasOnlyMatchingSamples(sample => sample.Labels.Has("thread id")).Should().BeTrue();
            output.EveryProfileHasOnlyMatchingSamples(sample => sample.Labels.GetRequired("thread id") == "1").Should().BeFalse();
        }

        [Fact]
        public void EveryProfileHasOnlyMatchingSamples_IsFalseWithoutProfilesOrWithAnEmptyProfile()
        {
            using var directory = new TempPprofDirectory();
            ProfileReader.Read(directory.DirectoryPath).EveryProfileHasOnlyMatchingSamples(_ => true).Should().BeFalse();

            new SyntheticProfile().SampleType("wall", "nanoseconds").WriteRaw(directory.DirectoryPath, "empty.pprof");
            ProfileReader.Read(directory.DirectoryPath).EveryProfileHasOnlyMatchingSamples(_ => true).Should().BeFalse();
        }
    }
}
