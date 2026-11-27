// <copyright file="ProfileLabelTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using FluentAssertions;
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

            var read = ProfileReader.ReadFile(path);
            var labels = read.Samples[0].Labels;
            labels.Count.Should().Be(4);
            labels[0].IsNumeric.Should().BeFalse();
            labels[0].Value.Should().Be("main");
            labels[2].IsNumeric.Should().BeTrue();
            labels[2].Number.Should().Be(42);
            labels[2].Unit.Should().Be("id");
            labels[2].Value.Should().Be("42");
            labels[3].IsNumeric.Should().BeTrue();
            labels[3].Value.Should().Be("0");

            labels.GetValues("thread name").Should().Equal("main", "worker");
            labels.GetRequired("span id").Should().Be("42");
            var duplicate = FluentActions.Invoking(() => labels.GetRequired("thread name")).Should().ThrowExactly<InvalidOperationException>().Which;
            duplicate.Message.Should().Contain(path);
            duplicate.Message.Should().Contain("main");
            duplicate.Message.Should().Contain("worker");

            labels.Should().HaveLabel("thread name", "worker");
            Action missing = () => labels.Should().HaveLabel("thread name", "other");
            missing.Should().Throw<Exception>()
                .Which.Message.Should().ContainAll("other", "main", "worker");
        }
    }
}
