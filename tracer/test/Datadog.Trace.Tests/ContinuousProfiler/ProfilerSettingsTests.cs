// <copyright file="ProfilerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ContinuousProfiler;

public class ProfilerSettingsTests : SettingsTestsBase
{
    // "auto" is a special profiling value that enables profiling based on heuristics
    // historically, we set a different value for profiling when we are in SSI. That is no longer
    // the case, but we keep the test here to ensure that remains the case!
    [Theory]
    [InlineData("1", null, (int)ProfilerState.Enabled)]
    [InlineData("0", null, (int)ProfilerState.Disabled)]
    [InlineData("true", null, (int)ProfilerState.Enabled)]
    [InlineData("false", null, (int)ProfilerState.Disabled)]
    [InlineData("auto", null, (int)ProfilerState.Auto)]
    [InlineData("1", "not used", (int)ProfilerState.Enabled)]
    [InlineData("0", "not used", (int)ProfilerState.Disabled)]
    [InlineData("true", "not used", (int)ProfilerState.Enabled)]
    [InlineData("false", "not used", (int)ProfilerState.Disabled)]
    [InlineData("auto", "not used", (int)ProfilerState.Auto)]
    [InlineData("invalid", "foo, profiler, bar", (int)ProfilerState.Disabled)]
    [InlineData("invalid", "profiler", (int)ProfilerState.Disabled)]
    [InlineData("invalid", "anything else", (int)ProfilerState.Disabled)]
    [InlineData("invalid", "", (int)ProfilerState.Disabled)]
    [InlineData("invalid", null, (int)ProfilerState.Disabled)]
    [InlineData("", "foo, profiler, bar", (int)ProfilerState.Disabled)]
    [InlineData("", "profiler", (int)ProfilerState.Disabled)]
    [InlineData("", "anything else", (int)ProfilerState.Disabled)]
    [InlineData("", "", (int)ProfilerState.Disabled)]
    [InlineData("", null, (int)ProfilerState.Disabled)]
    [InlineData(null, "foo, profiler, bar", (int)ProfilerState.Disabled)]
    [InlineData(null, "profiler", (int)ProfilerState.Disabled)]
    [InlineData(null, "anything else", (int)ProfilerState.Disabled)]
    [InlineData(null, null, (int)ProfilerState.Disabled)]
    [InlineData(null, "", (int)ProfilerState.Disabled)]
    public void ProfilerState_WhenPassedViaEnvironment(string profilingValue, string ssiValue, int expected)
    {
        var values = new List<(string, string)>();
        if (profilingValue is not null)
        {
            values.Add((Datadog.Trace.Configuration.ConfigurationKeys.Profiler.ProfilingEnabled, profilingValue));
        }

        if (ssiValue is not null)
        {
            values.Add((Datadog.Trace.Configuration.ConfigurationKeys.SsiDeployed, ssiValue));
        }

        var source = CreateConfigurationSource(values.ToArray());
        var settings = new ProfilerSettings(source, source, NullConfigurationTelemetry.Instance);

        // Whether the profiler is supported at all overrides the expected profiler state
        var expectedState = ProfilerSettings.IsProfilingSupported ? (ProfilerState)expected : ProfilerState.Disabled;
        settings.ProfilerState.Should().Be(expectedState);
    }

    [Theory]
    [InlineData("1", null, (int)ProfilerState.Enabled)]
    [InlineData("0", null, (int)ProfilerState.Disabled)]
    [InlineData("auto", null, (int)ProfilerState.Auto)]
    [InlineData(null, "1", (int)ProfilerState.Disabled)]
    [InlineData(null, "0", (int)ProfilerState.Disabled)]
    [InlineData(null, "auto", (int)ProfilerState.Disabled)]
    public void ProfilerState_WhenManagedActivationIsMissingOrOnByDefault_DontReadFromOnlyEnvVars(string configProfilingEnabled, string envProfilingEnabled, int expected)
    {
        var envValues = new List<(string, string)>();
        if (envProfilingEnabled is not null)
        {
            envValues.Add((Datadog.Trace.Configuration.ConfigurationKeys.Profiler.ProfilingEnabled, envProfilingEnabled));
        }

        var otherValues = new List<(string, string)>();
        if (configProfilingEnabled is not null)
        {
            otherValues.Add((Datadog.Trace.Configuration.ConfigurationKeys.Profiler.ProfilingEnabled, configProfilingEnabled));
        }

        var envConfig = CreateConfigurationSource(envValues.ToArray());
        var otherConfig = CreateConfigurationSource(otherValues.ToArray());

        // When profiler managed activation is enabled, the profiler state should be read from all config
        var settings = new ProfilerSettings(otherConfig, envConfig, NullConfigurationTelemetry.Instance);

        // Whether the profiler is supported at all overrides the expected profiler state
        var expectedState = ProfilerSettings.IsProfilingSupported ? (ProfilerState)expected : ProfilerState.Disabled;
        settings.ProfilerState.Should().Be(expectedState);
    }

    [Theory]
    [InlineData("1", null, (int)ProfilerState.Disabled)]
    [InlineData("0", null, (int)ProfilerState.Disabled)]
    [InlineData("auto", null, (int)ProfilerState.Disabled)]
    [InlineData(null, "1", (int)ProfilerState.Enabled)]
    [InlineData(null, "0", (int)ProfilerState.Disabled)]
    [InlineData(null, "auto", (int)ProfilerState.Auto)]
    public void ProfilerState_WhenManagedActivationIsDisabled_OnlyReadsFromEnvVars(string configProfilingEnabled, string envProfilingEnabled, int expected)
    {
        var envValues = new List<(string, string)>
        {
            (Datadog.Trace.Configuration.ConfigurationKeys.Profiler.ProfilerManagedActivationEnabled, "0")
        };

        if (envProfilingEnabled is not null)
        {
            envValues.Add((Datadog.Trace.Configuration.ConfigurationKeys.Profiler.ProfilingEnabled, envProfilingEnabled));
        }

        var otherValues = new List<(string, string)>();
        if (configProfilingEnabled is not null)
        {
            otherValues.Add((Datadog.Trace.Configuration.ConfigurationKeys.Profiler.ProfilingEnabled, configProfilingEnabled));
        }

        var envConfig = CreateConfigurationSource(envValues.ToArray());
        var otherConfig = CreateConfigurationSource(otherValues.ToArray());

        // When profiler managed activation is disabled, the profiler state should only be read from the environment variables
        var settings = new ProfilerSettings(otherConfig, envConfig, NullConfigurationTelemetry.Instance);

        // Whether the profiler is supported at all overrides the expected profiler state
        var expectedState = ProfilerSettings.IsProfilingSupported ? (ProfilerState)expected : ProfilerState.Disabled;
        settings.ProfilerState.Should().Be(expectedState);
    }

    [Fact]
    public void ProfilerState_IsProfilingSupported_OnlySupportedOnExpectedPlatforms()
    {
        // get the current runtime and platform
        var arch = RuntimeInformation.OSArchitecture;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        var isSupported = (arch, isWindows, isLinux) switch
        {
            (Architecture.X64, true, _) => true, // Windows x64
            (Architecture.X86, true, _) => true, // Windows x86
            (Architecture.X64, _, true) => true, // Linux x64
            _ => false // Unsupported platforms
        };

        ProfilerSettings.IsProfilingSupported.Should().Be(isSupported);
    }
}
