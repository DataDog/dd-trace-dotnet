// <copyright file="LoggerIntegrationCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Logging.ILogger;

public class LoggerIntegrationCommonTests
{
    [Fact]
    public async Task AddScope_LogInjectionDisabled()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.LogsInjectionEnabled, false },
                { ConfigurationKeys.Environment, "env" },
                { ConfigurationKeys.ServiceName, "serviceName" },
            });

        await using var tracer = TracerHelper.Create(settings);

        int callCount = 0;
        Action<object, State> callback = (_, _) =>
        {
            Interlocked.Increment(ref callCount);
        };
        LoggerIntegrationCommon.AddScope(tracer, callback, new State());

        callCount.Should().Be(0);
    }

    [Fact]
    public async Task AddScope_LogInjectionEnabled()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.LogsInjectionEnabled, true },
                { ConfigurationKeys.Environment, "env" },
                { ConfigurationKeys.ServiceName, "serviceName" },
            });

        await using var tracer = TracerHelper.Create(settings);

        Dictionary<string, object> values = null;
        Action<object, State> callback = (target, _) =>
        {
            var scope = target.Should().BeOfType<DatadogLoggingScope>().Subject;
            values = scope.ToDictionary(x => x.Key, x => x.Value);
        };

        LoggerIntegrationCommon.AddScope(tracer, callback, new State());

        values.Should()
              .NotBeNull()
              .And.ContainKey("dd_service")
              .And.ContainKey("dd_env");

        values["dd_service"].Should().Be("serviceName");
        values["dd_env"].Should().Be("env");
    }

    [Fact]
    public async Task AddScope_UpdatedSettings_LogInjectionEnabled()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.LogsInjectionEnabled, true },
                { ConfigurationKeys.Environment, "original_env" },
                { ConfigurationKeys.ServiceName, "original_serviceName" },
            });

        await using var tracer = TracerHelper.Create(settings);

        Dictionary<string, object> values = null;
        Action<object, State> callback = (target, _) =>
        {
            var scope = target.Should().BeOfType<DatadogLoggingScope>().Subject;
            values = scope.ToDictionary(x => x.Key, x => x.Value);
        };

        LoggerIntegrationCommon.AddScope(tracer, callback, new State());

        values.Should()
              .NotBeNull()
              .And.ContainKey("dd_service")
              .And.ContainKey("dd_env");

        values["dd_service"].Should().Be("original_serviceName");
        values["dd_env"].Should().Be("original_env");

        // update the settings
        tracer.Settings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new Dictionary<string, object>
                {
                    { TracerSettingKeyConstants.ServiceNameKey, "updated_service" },
                    { TracerSettingKeyConstants.EnvironmentKey, "updated_env" },
                },
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        // Should have new values
        LoggerIntegrationCommon.AddScope(tracer, callback, new State());

        values.Should()
              .NotBeNull()
              .And.ContainKey("dd_service")
              .And.ContainKey("dd_env");

        values["dd_service"].Should().Be("updated_service");
        values["dd_env"].Should().Be("updated_env");
    }

    private class State
    {
    }
}
