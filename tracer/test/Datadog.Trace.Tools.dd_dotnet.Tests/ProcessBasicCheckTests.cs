// <copyright file="ProcessBasicCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.Tests
{
    public class ProcessBasicCheckTests
    {
        [Theory]
        [InlineData("/app/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-musl-arm64/Datadog.Trace.ClrProfiler.Native.so")]
        public void DetectsBundleFromProfilerPathAlone(string profilerPath)
        {
            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath });

            result.Should().BeTrue();
        }

        [SkippableTheory]
        [InlineData(@"C:\app\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll")]
        [InlineData(@"C:\app\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll")]
        public void DetectsBundleFromProfilerPathAloneOnWindows(string profilerPath)
        {
            SkipOn.Platform(SkipOn.PlatformValue.Linux);
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath });

            result.Should().BeTrue();
        }

        [Fact]
        public void DetectsBundleRegardlessOfAppDirectory()
        {
            // GH-7214: the prefix here is arbitrary, matching a `dotnet app.dll` launch
            // where process.MainModule is the dotnet host, not the app's own directory.
            var profilerPath = "/usr/share/dotnet/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so";

            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath });

            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("/opt/datadog/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-x64/Datadog.Tracer.Native.so")]
        public void DoesNotDetectBundleForNonBundlePaths(string profilerPath)
        {
            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath });

            result.Should().BeFalse();
        }

        [Fact]
        public void DetectsBundleWhenAnyProfilerPathValueMatches()
        {
            string[] profilerPathValues =
            {
                null,
                "/opt/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                "/app/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
            };

            var result = ProcessBasicCheck.TracingWithBundle(profilerPathValues);

            result.Should().BeTrue();
        }
    }
}
