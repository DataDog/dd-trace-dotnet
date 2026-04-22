// <copyright file="ServerlessCompatPipeNameHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Serverless
{
    public class ServerlessCompatPipeNameHelperTests
    {
        [Fact]
        public void IsCompatLayerAvailableWithPipeSupport_DoesNotThrow()
        {
            // Should never throw regardless of environment — errors are caught internally
            var act = () => ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(compatPathOverride: null);

            act.Should().NotThrow();
        }

        [SkippableFact]
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsFalse_WhenNotOnWindows()
        {
            SkipOn.Platform(SkipOn.PlatformValue.Windows);

            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(compatPathOverride: null);

            result.Should().BeFalse();
        }

        [SkippableFact]
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsFalse_WhenCompatBinaryMissing()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            // Binary missing, DLL present
            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(
                compatPathOverride: null,
                fileExists: path => !path.EndsWith(".exe"),
                getAssemblyVersion: _ => new Version(1, 4, 0));

            result.Should().BeFalse();
        }

        [SkippableFact]
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsFalse_WhenCompatDllMissing()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            // Binary present, DLL missing
            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(
                compatPathOverride: null,
                fileExists: path => !path.EndsWith(".dll"),
                getAssemblyVersion: _ => new Version(1, 4, 0));

            result.Should().BeFalse();
        }

        [SkippableFact]
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsFalse_WhenVersionIsNull()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(
                compatPathOverride: null,
                fileExists: _ => true,
                getAssemblyVersion: _ => null);

            result.Should().BeFalse();
        }

        [SkippableTheory]
        [InlineData(1, 3, 0)]
        [InlineData(1, 0, 0)]
        [InlineData(0, 1, 0)]
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsFalse_WhenVersionTooOld(int major, int minor, int build)
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(
                compatPathOverride: null,
                fileExists: _ => true,
                getAssemblyVersion: _ => new Version(major, minor, build));

            result.Should().BeFalse();
        }

        [SkippableTheory]
        [InlineData(0, 0, 0)]  // dev build
        [InlineData(1, 4, 0)]  // minimum supported
        [InlineData(1, 5, 0)]  // above minimum
        [InlineData(2, 0, 0)]  // major version bump
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsTrue_WhenVersionSupported(int major, int minor, int build)
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(
                compatPathOverride: null,
                fileExists: _ => true,
                getAssemblyVersion: _ => new Version(major, minor, build));

            result.Should().BeTrue();
        }

        [SkippableFact]
        public void IsCompatLayerAvailableWithPipeSupport_ReturnsFalse_WhenGetVersionThrows()
        {
            SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

            var result = ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(
                compatPathOverride: null,
                fileExists: _ => true,
                getAssemblyVersion: _ => throw new BadImageFormatException("not a valid assembly"));

            result.Should().BeFalse();
        }
    }
}
