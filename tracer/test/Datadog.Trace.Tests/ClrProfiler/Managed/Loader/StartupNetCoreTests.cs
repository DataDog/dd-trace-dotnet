// <copyright file="StartupNetCoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader
{
    public class StartupNetCoreTests
    {
        [Fact]
        public void ComputeTfmDirectory_WithTracerHomeDirectory_ReturnsCorrectTfm()
        {
            string tracerHome;
            string expectedTfm;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                tracerHome = @"C:\path\to\tracer";
            }
            else
            {
                tracerHome = "/path/to/tracer";
            }

            // Determine TFM based on Environment.Version, matching the logic in Startup.ComputeTfmDirectory
            var version = Environment.Version;

            if (version.Major >= 6)
            {
                expectedTfm = "net6.0";
            }
            else if (version is { Major: 3, Minor: >= 1 } || version.Major == 5)
            {
                expectedTfm = "netcoreapp3.1";
            }
            else
            {
                expectedTfm = "netstandard2.0";
            }

            var expectedDirectory = Path.Combine(tracerHome, expectedTfm);
            Startup.ComputeTfmDirectory(tracerHome).Should().Be(expectedDirectory);
        }

        [SkippableTheory]
        [InlineData(Architecture.X64, "CORECLR_PROFILER_PATH_64")]
        [InlineData(Architecture.X86, "CORECLR_PROFILER_PATH_32")]
        [InlineData(Architecture.Arm64, "CORECLR_PROFILER_PATH_ARM64")]
        [InlineData(Architecture.Arm, "CORECLR_PROFILER_PATH_ARM")]
        public void GetProfilerPathEnvVarNameForArch_ReturnsCorrectName(Architecture architecture, string expected)
        {
            // Skip the test if the current architecture doesn't match
            Skip.If(RuntimeInformation.ProcessArchitecture != architecture, $"Skipping test for {architecture}");

            Startup.GetProfilerPathEnvVarNameForArch().Should().Be(expected);
        }

        [Fact]
        public void GetProfilerPathEnvVarNameFallback_ReturnsCorrectName()
        {
            Startup.GetProfilerPathEnvVarNameFallback().Should().Be("CORECLR_PROFILER_PATH");
        }

        [Fact]
        public void GetTracerHomePath_WithTracerHomeEnvVar_ReturnsValue()
        {
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", "/path/to/tracer");

            Startup.GetTracerHomePath(envVars).Should().Be("/path/to/tracer");
        }

        [Fact]
        public void GetTracerHomePath_WithTracerHomeEnvVar_TrimsWhitespace()
        {
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", "  /path/to/tracer  ");

            Startup.GetTracerHomePath(envVars).Should().Be("/path/to/tracer");
        }

        [Fact]
        public void GetTracerHomePath_WithoutTracerHomeEnvVar_UsesArchProfilerPath()
        {
            var tracerHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\path\to\tracer" : "/path/to/tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

            var archEnvVar = Startup.GetProfilerPathEnvVarNameForArch();
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable(archEnvVar, profilerPath);

            Startup.GetTracerHomePath(envVars).Should().Be(tracerHome);
        }

        [Fact]
        public void GetTracerHomePath_WithoutTracerHomeEnvVar_UsesFallbackProfilerPath()
        {
            var tracerHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\path\to\tracer" : "/path/to/tracer";
            var profilerPath = Path.Combine(tracerHome, "linux-x64", "Datadog.Trace.ClrProfiler.Native.so");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("CORECLR_PROFILER_PATH", profilerPath);

            Startup.GetTracerHomePath(envVars).Should().Be(tracerHome);
        }

        [Fact]
        public void GetTracerHomePath_WithNoEnvironmentVariables_ReturnsNull()
        {
            var envVars = new MockEnvironmentVariableProvider();

            Startup.GetTracerHomePath(envVars).Should().BeNull();
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_WithArchDirectory_ReturnsTracerHome()
        {
            var tracerHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\path\to\tracer" : "/path/to/tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", profilerPath);

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }

        [Theory]
        [InlineData("win-x64")]
        [InlineData("win-x86")]
        [InlineData("linux-x64")]
        [InlineData("linux-arm64")]
        [InlineData("linux-musl-x64")]
        [InlineData("linux-musl-arm64")]
        [InlineData("osx")]
        [InlineData("osx-arm64")]
        [InlineData("osx-x64")]
        public void ComputeTracerHomePathFromProfilerPath_WithAllArchDirectories_ReturnsTracerHome(string archDir)
        {
            var tracerHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\path\to\tracer" : "/path/to/tracer";
            var profilerPath = Path.Combine(tracerHome, archDir, "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", profilerPath);

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_WithoutArchDirectory_ReturnsParentDirectory()
        {
            var tracerHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\path\to\tracer" : "/path/to/tracer";
            var profilerPath = Path.Combine(tracerHome, "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", profilerPath);

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_WithEmptyValue_ReturnsNull()
        {
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", string.Empty);

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().BeNull();
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_WithWhitespaceValue_ReturnsNull()
        {
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", "   ");

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().BeNull();
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_WithMissingVariable_ReturnsNull()
        {
            var envVars = new MockEnvironmentVariableProvider();

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().BeNull();
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_TrimsWhitespace()
        {
            var tracerHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\path\to\tracer" : "/path/to/tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", $"  {profilerPath}  ");

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }
    }
}

#endif
