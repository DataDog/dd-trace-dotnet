// <copyright file="ProfilerSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ContinuousProfiler;

public class ProfilerSettingsTests : SettingsTestsBase
{
    // profiling takes precedence over SSI
    // "auto" is a special profiling value that enables profiling when deployed via SSI
    // the profiler will also be enabled when "profiler" will be added to the DD_INJECTION_ENABLED environment variable
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
    [InlineData("invalid", "foo, profiler, bar", (int)ProfilerState.Auto)]
    [InlineData("invalid", "profiler", (int)ProfilerState.Auto)]
    [InlineData("invalid", "anything else", (int)ProfilerState.Disabled)]
    [InlineData("invalid", "", (int)ProfilerState.Disabled)]
    [InlineData("invalid", null, (int)ProfilerState.Disabled)]
    [InlineData("", "foo, profiler, bar", (int)ProfilerState.Auto)]
    [InlineData("", "profiler", (int)ProfilerState.Auto)]
    [InlineData("", "anything else", (int)ProfilerState.Disabled)]
    [InlineData("", "", (int)ProfilerState.Disabled)]
    [InlineData("", null, (int)ProfilerState.Disabled)]
    [InlineData(null, "foo, profiler, bar", (int)ProfilerState.Auto)]
    [InlineData(null, "profiler", (int)ProfilerState.Auto)]
    [InlineData(null, "anything else", (int)ProfilerState.Disabled)]
    [InlineData(null, null, (int)ProfilerState.Disabled)]
    [InlineData(null, "", (int)ProfilerState.Disabled)]
    public void ProfilerState_WhenPassedViaEnvironment(string profilingValue, string ssiValue, int expected)
    {
        var values = new List<(string, string)>();
        if (profilingValue is not null)
        {
            values.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilingEnabled, profilingValue));
        }

        if (ssiValue is not null)
        {
            values.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.SsiDeployed, ssiValue));
        }

        var source = CreateConfigurationSource(values.ToArray());
        var settings = new ProfilerSettings(source, source, NullConfigurationTelemetry.Instance);

        settings.ProfilerState.Should().Be((ProfilerState)expected);
    }

    [Theory]
    [InlineData("1", null, (int)ProfilerState.Enabled)]
    [InlineData("0", null, (int)ProfilerState.Disabled)]
    [InlineData("auto", null, (int)ProfilerState.Auto)]
    [InlineData(null, "1", (int)ProfilerState.Disabled)]
    [InlineData(null, "0", (int)ProfilerState.Disabled)]
    [InlineData(null, "auto", (int)ProfilerState.Disabled)]
    public void ProfilerState_WhenManagedActivationIsEnabledOrMissing_ReadsFromAllConfig(string configProfilingEnabled, string envProfilingEnabled, int expected)
    {
        var envValues = new List<(string, string)>();
        if (envProfilingEnabled is not null)
        {
            envValues.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilingEnabled, envProfilingEnabled));
        }

        var otherValues = new List<(string, string)>();
        if (configProfilingEnabled is not null)
        {
            otherValues.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilingEnabled, configProfilingEnabled));
        }

        var envConfig = CreateConfigurationSource(envValues.ToArray());
        var otherConfig = CreateConfigurationSource(otherValues.ToArray());

        // When profiler managed activation is disabled, the profiler state should only be read from the environment variables
        var settings = new ProfilerSettings(otherConfig, envConfig, NullConfigurationTelemetry.Instance);
        settings.ProfilerState.Should().Be((ProfilerState)expected);
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
            (Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilerManagedActivationEnabled, "0")
        };

        if (envProfilingEnabled is not null)
        {
            envValues.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilingEnabled, envProfilingEnabled));
        }

        var otherValues = new List<(string, string)>();
        if (configProfilingEnabled is not null)
        {
            otherValues.Add((Datadog.Trace.ContinuousProfiler.ConfigurationKeys.ProfilingEnabled, configProfilingEnabled));
        }

        var envConfig = CreateConfigurationSource(envValues.ToArray());
        var otherConfig = CreateConfigurationSource(otherValues.ToArray());

        // When profiler managed activation is disabled, the profiler state should only be read from the environment variables
        var settings = new ProfilerSettings(otherConfig, envConfig, NullConfigurationTelemetry.Instance);
        settings.ProfilerState.Should().Be((ProfilerState)expected);
    }
}
