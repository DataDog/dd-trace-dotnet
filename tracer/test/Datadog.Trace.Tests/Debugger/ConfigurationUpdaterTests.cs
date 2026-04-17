// <copyright file="ConfigurationUpdaterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.RemoteConfigurationManagement;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ConfigurationUpdaterTests
{
    [Fact]
    public void AcceptAdded_ServiceConfigurationOnly_UpdatesGlobalRateLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock();
        var updater = ConfigurationUpdater.Create("env", "version", 0, globalRateLimiter);

        updater.AcceptAdded(
            new ProbeConfiguration
            {
                ServiceConfiguration = new ServiceConfiguration
                {
                    Sampling = new Datadog.Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 42 }
                }
            });

        Assert.Equal(1, globalRateLimiter.SetRateCallCount);
        Assert.Equal(42, globalRateLimiter.LastRate);
    }

    [Fact]
    public void AcceptAdded_ProbeOnlyChange_DoesNotResetGlobalRateLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock();
        var updater = ConfigurationUpdater.Create("env", "version", 0, globalRateLimiter);
        updater.AcceptAdded(
            new ProbeConfiguration
            {
                ServiceConfiguration = new ServiceConfiguration
                {
                    Sampling = new Datadog.Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 42 }
                }
            });
        globalRateLimiter.ResetCounters();

        updater.AcceptAdded(
            new ProbeConfiguration
            {
                ServiceConfiguration = new ServiceConfiguration
                {
                    Sampling = new Datadog.Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 42 }
                },
                LogProbes =
                [
                    new LogProbe
                    {
                        Id = "log-probe",
                        Where = new Where { MethodName = "TestMethod" },
                        Tags = [],
                    }
                ]
            });

        Assert.Equal(0, globalRateLimiter.SetRateCallCount);
        Assert.Equal(0, globalRateLimiter.ResetRateCallCount);
    }

    [Fact]
    public void AcceptRemoved_ServiceConfiguration_ResetsGlobalRateLimiter()
    {
        var globalRateLimiter = new GlobalRateLimiterMock();
        var updater = ConfigurationUpdater.Create("env", "version", 0, globalRateLimiter);
        updater.AcceptAdded(
            new ProbeConfiguration
            {
                ServiceConfiguration = new ServiceConfiguration
                {
                    Sampling = new Datadog.Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 42 }
                }
            });
        globalRateLimiter.ResetCounters();

        updater.AcceptRemoved([RemoteConfigurationPath.FromPath("datadog/123/LIVE_DEBUGGING/serviceConfig_/config")]);

        Assert.Equal(0, globalRateLimiter.SetRateCallCount);
        Assert.Equal(1, globalRateLimiter.ResetRateCallCount);
    }

    private sealed class GlobalRateLimiterMock : IDebuggerGlobalRateLimiter
    {
        public double? LastRate { get; private set; }

        public int SetRateCallCount { get; private set; }

        public int ResetRateCallCount { get; private set; }

        public bool ShouldSample(ProbeType probeType, string probeId) => true;

        public void SetRate(double? samplesPerSecond)
        {
            SetRateCallCount++;
            LastRate = samplesPerSecond;
        }

        public void ResetRate()
        {
            ResetRateCallCount++;
        }

        public void ResetCounters()
        {
            SetRateCallCount = 0;
            ResetRateCallCount = 0;
        }

        public void Dispose()
        {
        }
    }
}
