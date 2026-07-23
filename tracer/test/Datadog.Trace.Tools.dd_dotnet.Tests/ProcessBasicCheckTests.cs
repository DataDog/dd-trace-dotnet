// <copyright file="ProcessBasicCheckTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.Shared;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.Tests
{
    public class ProcessBasicCheckTests
    {
        [SkippableTheory]
        [InlineData("/app/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-musl-arm64/Datadog.Trace.ClrProfiler.Native.so")]
        public void DetectsBundleWhenMainModuleDirectoryMatchesAppDirectory(string profilerPath)
        {
            // Path.GetDirectoryName normalizes the root separator of a forward-slash path
            // differently on real Windows, so this Unix-style exact-match scenario only holds
            // on Linux/macOS - see DetectsBundleWhenMainModuleDirectoryMatchesAppDirectoryOnWindows.
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            var process = CreateProcessInfo(mainModuleDirectory: "/app");

            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath }, process, out var usedFallback);

            result.Should().BeTrue();
            usedFallback.Should().BeFalse();
        }

        [SkippableTheory]
        [InlineData(@"C:\app\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll")]
        [InlineData(@"C:\app\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll")]
        public void DetectsBundleWhenMainModuleDirectoryMatchesAppDirectoryOnWindows(string profilerPath)
        {
            SkipOn.Platform(SkipOn.PlatformValue.Linux);
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var process = CreateProcessInfo(mainModuleDirectory: @"C:\app");

            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath }, process, out var usedFallback);

            result.Should().BeTrue();
            usedFallback.Should().BeFalse();
        }

        [Fact]
        public void DetectsBundleWhenLaunchedAsDotnetDll()
        {
            // GH-7214: `dotnet app.dll` launches (e.g. Azure App Service Linux) report the
            // dotnet host as MainModule, not the app's own directory.
            var process = CreateProcessInfo(mainModuleDirectory: "/usr/share/dotnet");
            var profilerPath = "/app/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so";

            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath }, process, out var usedFallback);

            result.Should().BeTrue();
            usedFallback.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("/opt/datadog/Datadog.Trace.ClrProfiler.Native.so")]
        [InlineData("/app/datadog/linux-x64/Datadog.Tracer.Native.so")]
        public void DoesNotDetectBundleForNonBundlePaths(string profilerPath)
        {
            var process = CreateProcessInfo(mainModuleDirectory: "/app");

            var result = ProcessBasicCheck.TracingWithBundle(new[] { profilerPath }, process, out var usedFallback);

            result.Should().BeFalse();
            usedFallback.Should().BeFalse();
        }

        [SkippableFact]
        public void DetectsBundleWhenAnyProfilerPathValueMatches()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            var process = CreateProcessInfo(mainModuleDirectory: "/app");
            string[] profilerPathValues =
            {
                null,
                "/opt/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                "/app/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
            };

            var result = ProcessBasicCheck.TracingWithBundle(profilerPathValues, process, out var usedFallback);

            result.Should().BeTrue();
            usedFallback.Should().BeFalse();
        }

        private static ProcessInfo CreateProcessInfo(string mainModuleDirectory)
        {
            return new ProcessInfo(
                "app",
                1,
                new Dictionary<string, string>(),
                mainModule: $"{mainModuleDirectory}/app",
                modules: System.Array.Empty<string>());
        }
    }
}
