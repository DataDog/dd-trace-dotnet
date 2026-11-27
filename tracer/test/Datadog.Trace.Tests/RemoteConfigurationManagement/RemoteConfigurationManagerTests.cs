// <copyright file="RemoteConfigurationManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RemoteConfigurationManagerTests
{
    [Fact]
    public void AgentChangesReuseOneDiscoverySubscriptionAndDisposalRemovesIt()
    {
        var telemetry = new ConfigurationTelemetry();
        var source = new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:8126",
        });
        var settings = new TracerSettings(source, telemetry);
        var discovery = new DiscoveryServiceMock();
        var manager = RemoteConfigurationManager.Create(
            discovery, RemoteConfigurationSettings.FromSource(source, telemetry), settings, new NullGitMetadataProvider(), new RcmSubscriptionManager());
        try
        {
            var initialCount = discovery.Callbacks.Count;
            for (var port = 8127; port <= 8129; port++)
            {
                var manual = new ManualInstrumentationConfigurationSource(
                    new Dictionary<string, object> { ["DD_TRACE_AGENT_URL"] = new Uri($"http://127.0.0.1:{port}") },
                    useDefaultSources: true);
                settings.Manager.UpdateManualConfigurationSettings(manual, telemetry).Should().BeTrue();
            }

            discovery.Callbacks.Should().HaveCount(initialCount);
            discovery.Callbacks.Should().ContainSingle();
        }
        finally
        {
            manager.Dispose();
        }

        discovery.Callbacks.Should().BeEmpty();
    }
}
