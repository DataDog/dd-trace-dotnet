// <copyright file="ProfileLabelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Perftools.Profiles;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class ProfileLabelTests
    {
        [Fact]
        public void Labels_KeepDuplicateStringAndNumericValues()
        {
            using var directory = new TempPprofDirectory();
            var path = new SyntheticProfile()
                .SampleType("cpu", "nanoseconds")
                .AddSample(
                    new long[] { 1 },
                    Array.Empty<ulong>(),
                    SyntheticProfile.SampleLabel.String("thread name", "main"),
                    SyntheticProfile.SampleLabel.String("thread name", "worker"),
                    SyntheticProfile.SampleLabel.Numeric("span id", 42, "id"),
                    SyntheticProfile.SampleLabel.Numeric("count", 0, "count", "ignored"))
                .WriteRaw(directory.DirectoryPath, "profile.pprof");

            Profile legacy;
            using (var stream = File.OpenRead(path))
            {
                legacy = Profile.Parser.ParseFrom(stream);
            }

            var legacyLabels = legacy.Sample[0].Labels(legacy).ToArray();
            legacyLabels.Select(label => label.Name + "=" + label.Value).Should().Equal(
                "thread name=main",
                "thread name=worker",
                "span id=42",
                "count=0");

            var read = ProfileReader.ReadFile(path);
            var labels = read.Samples[0].Labels;
            labels.Count.Should().Be(4);
            labels[0].IsNumeric.Should().BeFalse();
            labels[0].Value.Should().Be("main");
            labels[2].IsNumeric.Should().BeTrue();
            labels[2].Number.Should().Be(42);
            labels[2].Unit.Should().Be("id");
            labels[2].Value.Should().Be(legacyLabels[2].Value);
            labels[3].IsNumeric.Should().BeTrue();
            labels[3].Value.Should().Be("0");
            labels[3].Value.Should().Be(legacyLabels[3].Value);

            labels.GetValues("thread name").Should().Equal("main", "worker");
            labels.GetRequired("span id").Should().Be("42");
            var duplicate = Assert.Throws<InvalidOperationException>(() => labels.GetRequired("thread name"));
            duplicate.Message.Should().Contain(path);
            duplicate.Message.Should().Contain("main");
            duplicate.Message.Should().Contain("worker");

            labels.Should().HaveLabel("thread name", "worker");
            Action missing = () => labels.Should().HaveLabel("thread name", "other");
            missing.Should().Throw<Exception>()
                .Which.Message.Should().Contain("other")
                .And.Contain("main")
                .And.Contain("worker");
        }
    }
}
