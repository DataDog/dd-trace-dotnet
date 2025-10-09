// <copyright file="ConfigureIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ManualInstrumentation;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class ConfigureIntegrationTests
{
    [Fact]
    public void ConfigureSettingsWithManualOverrides_DoesNotThrow_WhenTracerIsLocked()
    {
        // Arrange: lock the global tracer manager
        TracerManager.UnsafeReplaceGlobalManager(new LockedTracerManager());
        TracerManager.Instance.Should().BeAssignableTo<ILockedTracer>();

        // Act/Assert: should not throw and should not replace the manager
        ConfigureIntegration.ConfigureSettingsWithManualOverrides(new Dictionary<string, object?>(), useLegacySettings: false);

        TracerManager.Instance.Should().BeAssignableTo<ILockedTracer>();
    }

    private class LockedTracerManager : TracerManager, ILockedTracer
    {
        public LockedTracerManager()
            : base(new TracerSettings(), null, null, null, null, null, null, null, null, null, null, null, null, Mock.Of<IRemoteConfigurationManager>(), Mock.Of<IDynamicConfigurationManager>(), Mock.Of<ITracerFlareManager>(), Mock.Of<ISpanEventsManager>())
        {
        }
    }
}
