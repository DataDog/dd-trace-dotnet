// <copyright file="CiStageSelectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.CiStageSelectionTests
{
    public class CiStageSelectionTests
    {
        [Fact]
        public void ProfilerSourceOnly()
        {
            AssertSelection(["profiler/src/foo.cpp"], skipTracerOnlyStages: true, libdatadogChanged: false);
        }

        [Fact]
        public void LibdatadogCmakeOnly()
        {
            AssertSelection(["build/cmake/FindLibdatadog.cmake"], skipTracerOnlyStages: true, libdatadogChanged: true);
        }

        [Fact]
        public void LibdatadogPortOnly()
        {
            AssertSelection(["build/vcpkg_local_ports/libdatadog/vcpkg.json"], skipTracerOnlyStages: true, libdatadogChanged: true);
        }

        [Fact]
        public void ProfilerManagedTestOnly()
        {
            AssertSelection(
                ["tracer/test/Datadog.Trace.Tests/ContinuousProfiler/ProfilerTests.cs"],
                skipTracerOnlyStages: true,
                libdatadogChanged: false);
        }

        [Fact]
        public void ContinuousProfilerSourceOnly()
        {
            AssertSelection(
                ["tracer/src/Datadog.Trace/ContinuousProfiler/Profiler.cs"],
                skipTracerOnlyStages: false,
                libdatadogChanged: false);
        }

        [Fact]
        public void ProfilerAndTracerSource()
        {
            AssertSelection(
                ["profiler/src/foo.cpp", "tracer/src/Datadog.Trace/Tracer.cs"],
                skipTracerOnlyStages: false,
                libdatadogChanged: false);
        }

        [Fact]
        public void ProfilerSourceAndIgnoredDocs()
        {
            AssertSelection(
                ["profiler/src/foo.cpp", "docs/development/ci/notes.md"],
                skipTracerOnlyStages: true,
                libdatadogChanged: false);
        }

        [Fact]
        public void ProfilerSourceAndLibdatadogPin()
        {
            AssertSelection(
                ["profiler/src/foo.cpp", "build/cmake/FindLibdatadog.cmake"],
                skipTracerOnlyStages: true,
                libdatadogChanged: true);
        }

        [Fact]
        public void ReadmeOnly()
        {
            AssertSelection(["docs/README.md"], skipTracerOnlyStages: false, libdatadogChanged: false);
        }

        [Fact]
        public void EmptyList()
        {
            AssertSelection([], skipTracerOnlyStages: false, libdatadogChanged: false);
        }

        [Fact]
        public void NativeTracerSource()
        {
            AssertSelection(
                ["shared/src/Datadog.Trace.ClrProfiler.Native/foo.cpp"],
                skipTracerOnlyStages: false,
                libdatadogChanged: false);
        }

        [Fact]
        public void ProfilerBuildSteps()
        {
            AssertSelection(
                ["tracer/build/_build/Build.Profiler.Steps.cs"],
                skipTracerOnlyStages: false,
                libdatadogChanged: false);
        }

        [Fact]
        public void TracerFileMovedIntoProfiler()
        {
            AssertSelection(
                ["tracer/src/Datadog.Trace/Foo.cs", "profiler/src/Foo.cs"],
                skipTracerOnlyStages: false,
                libdatadogChanged: false);
        }

        private static void AssertSelection(string[] changedPaths, bool skipTracerOnlyStages, bool libdatadogChanged)
        {
            var result = CiStageSelection.Select(changedPaths);

            using (new AssertionScope())
            {
                result.SkipTracerOnlyStages.Should().Be(skipTracerOnlyStages);
                result.LibdatadogChanged.Should().Be(libdatadogChanged);
            }
        }
    }
}
