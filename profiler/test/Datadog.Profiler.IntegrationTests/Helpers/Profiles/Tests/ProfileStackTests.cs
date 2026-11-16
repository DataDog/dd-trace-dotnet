// <copyright file="ProfileStackTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using FluentAssertions;
using Perftools.Profiles;
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
            Profile legacy;
            using (var stream = File.OpenRead(path))
            {
                legacy = Profile.Parser.ParseFrom(stream);
            }

            var legacyLeaf = legacy.Sample[0].StackTrace(legacy);
            legacyLeaf.FramesCount.Should().Be(2);
            legacyLeaf[0].Function.Should().Be("Leaf");
            legacyLeaf[0].Line.Should().Be(8);
            legacyLeaf[0].Filename.Should().Be("Leaf.cs");
            legacyLeaf[1].Function.Should().Be("Caller");

            var legacyInline = legacy.Sample[1].StackTrace(legacy);
            legacyInline.FramesCount.Should().Be(1);
            legacyInline[0].Function.Should().Be("Inlinee");
            legacyInline[0].Line.Should().Be(10);
            legacyInline[0].Filename.Should().Be("Inlinee.cs");

            var read = ProfileReader.ReadFile(path);
            var leafStack = read.Samples[0].CallStack;
            leafStack.FramesCount.Should().Be(legacyLeaf.FramesCount);
            leafStack[0].Function.Should().Be(legacyLeaf[0].Function);
            leafStack[0].Line.Should().Be(legacyLeaf[0].Line);
            leafStack[0].Filename.Should().Be(legacyLeaf[0].Filename);
            leafStack[1].Function.Should().Be(legacyLeaf[1].Function);
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
