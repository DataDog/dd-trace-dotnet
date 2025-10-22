// <copyright file="ServiceDiscoveryHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.LibDatadog.ServiceDiscovery;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.ServiceDiscovery;

public class ServiceDiscoveryHelperTests
{
    [SkippableFact]
    public void ShouldSkipStoringMetadataOnWindowsAndMac()
    {
        // We do store metadata for linux - so this test doesn't apply
        SkipOn.Platform(SkipOn.PlatformValue.Linux);

        var tracerSettings = TracerSettings.Create([]);
        var mutableSettings = MutableSettings.CreateForTesting(tracerSettings, []);
        var result = ServiceDiscoveryHelper.StoreTracerMetadata(tracerSettings, mutableSettings);

        result.Should().Be(ServiceDiscoveryHelper.StoreMetadataResult.Skipped);
    }

    [SkippableFact]
    public void ShouldSkipStoringMetadataInUninstrumentedProcess()
    {
        // We only potentially store on linux
        SkipOn.Platform(SkipOn.PlatformValue.Windows);
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);

        var tracerSettings = TracerSettings.Create([]);
        var mutableSettings = MutableSettings.CreateForTesting(tracerSettings, []);
        var result = ServiceDiscoveryHelper.StoreTracerMetadata(tracerSettings, mutableSettings);

        // We are not instrumenting the test process, so we expect the result to be Skipped
        result.Should().Be(ServiceDiscoveryHelper.StoreMetadataResult.Skipped);
    }
}
