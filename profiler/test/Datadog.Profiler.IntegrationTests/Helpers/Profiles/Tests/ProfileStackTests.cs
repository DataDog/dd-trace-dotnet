// <copyright file="ProfileStackTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class ProfileStackTests
    {
        [Fact]
        public void Stacks_FollowLocationIdOrder_AndKeepOnlyTheFirstInlinedLine()
        {
            var profile = new SyntheticProfile().SampleType("cpu", "nanoseconds");
            var caller = profile.Function("Caller", "Caller.cs");
            var leaf = profile.Function("Leaf", "Leaf.cs");
            var inlinee = profile.Function("Inlinee", "Inlinee.cs");
            var outer = profile.Function("Outer", "Outer.cs");
            var callerLocation = profile.Location(caller, 2);
            var leafLocation = profile.Location(leaf, 8);
            var inlined = profile.InlinedLocation(inlinee, 10, outer, 20);
            profile.AddSample(new long[] { 1 }, new[] { leafLocation, callerLocation });
            profile.AddSample(new long[] { 1 }, new[] { inlined });

            using var directory = new TempPprofDirectory();
            var path = profile.WriteRaw(directory.DirectoryPath, "profile.pprof");
            var read = ProfileReader.ReadFile(path);
            var leafStack = read.Samples[0].CallStack;
            leafStack.FramesCount.Should().Be(2);
            leafStack[0].Function.Should().Be("Leaf");
            leafStack[0].Line.Should().Be(8);
            leafStack[0].Filename.Should().Be("Leaf.cs");
            leafStack[1].Function.Should().Be("Caller");
            leafStack[1].Line.Should().Be(2);
            leafStack[1].Filename.Should().Be("Caller.cs");

            var inlineStack = read.Samples[1].CallStack;
            inlineStack.FramesCount.Should().Be(1);
            inlineStack[0].Function.Should().Be("Inlinee");
            inlineStack[0].Line.Should().Be(10);
            inlineStack[0].Filename.Should().Be("Inlinee.cs");
            read.Samples.Should().HaveCount(2);
        }
    }
}
