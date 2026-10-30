// <copyright file="StartupNetFrameworkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using System;
using System.IO;
using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader
{
    public class StartupNetFrameworkTests
    {
        [Fact]
        public void ComputeTfmDirectory_WithTracerHomeDirectory_ReturnsCorrectTfm()
        {
            const string tracerHome = @"C:\path\to\tracer";
            const string expectedDirectory = @"C:\path\to\tracer\net461";

            Startup.ComputeTfmDirectory(tracerHome).Should().Be(expectedDirectory);
        }

        [Fact]
        public void GetProfilerPathEnvVarNameForArch_ReturnsCorrectName()
        {
            if (Environment.Is64BitProcess)
            {
                Startup.GetProfilerPathEnvVarNameForArch().Should().Be("COR_PROFILER_PATH_64");
            }
            else
            {
                Startup.GetProfilerPathEnvVarNameForArch().Should().Be("COR_PROFILER_PATH_32");
            }
        }

        [Fact]
        public void GetProfilerPathEnvVarNameFallback_ReturnsCorrectName()
        {
            Startup.GetProfilerPathEnvVarNameFallback().Should().Be("COR_PROFILER_PATH");
        }

        [Fact]
        public void GetTracerHomePath_WithTracerHomeEnvVar_ReturnsValue()
        {
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", @"C:\path\to\tracer");

            Startup.GetTracerHomePath(envVars).Should().Be(@"C:\path\to\tracer");
        }

        [Fact]
        public void GetTracerHomePath_WithTracerHomeEnvVar_TrimsWhitespace()
        {
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", @"  C:\path\to\tracer  ");

            Startup.GetTracerHomePath(envVars).Should().Be(@"C:\path\to\tracer");
        }

        [Fact]
        public void GetTracerHomePath_WithoutTracerHomeEnvVar_UsesArchProfilerPath()
        {
            const string tracerHome = @"C:\path\to\tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

            var archEnvVar = Startup.GetProfilerPathEnvVarNameForArch();
            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable(archEnvVar, profilerPath);

            Startup.GetTracerHomePath(envVars).Should().Be(tracerHome);
        }

        [Fact]
        public void GetTracerHomePath_WithoutTracerHomeEnvVar_UsesFallbackProfilerPath()
        {
            const string tracerHome = @"C:\path\to\tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("COR_PROFILER_PATH", profilerPath);

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
            const string tracerHome = @"C:\path\to\tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", profilerPath);

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }

        [Theory]
        [InlineData("win-x64")]
        [InlineData("win-x86")]
        public void ComputeTracerHomePathFromProfilerPath_WithAllArchDirectories_ReturnsTracerHome(string archDir)
        {
            const string tracerHome = @"C:\path\to\tracer";
            var profilerPath = Path.Combine(tracerHome, archDir, "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", profilerPath);

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }

        [Fact]
        public void ComputeTracerHomePathFromProfilerPath_WithoutArchDirectory_ReturnsParentDirectory()
        {
            const string tracerHome = @"C:\path\to\tracer";
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
            const string tracerHome = @"C:\path\to\tracer";
            var profilerPath = Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");

            var envVars = new MockEnvironmentVariableProvider();
            envVars.SetEnvironmentVariable("TEST_VAR", $"  {profilerPath}  ");

            Startup.ComputeTracerHomePathFromProfilerPath(envVars, "TEST_VAR").Should().Be(tracerHome);
        }
    }
}

#endif
