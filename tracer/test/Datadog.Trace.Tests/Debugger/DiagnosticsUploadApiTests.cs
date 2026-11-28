// <copyright file="DiagnosticsUploadApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DiagnosticsUploadApiTests
{
    private readonly TestRequestFactory _testRequestFactory;
    private readonly DiscoveryServiceMock _discoveryServiceMock;
    private readonly NullGitMetadataProvider _nullGitMetadataProvider;
    private readonly ArraySegment<byte> _data;

    public DiagnosticsUploadApiTests()
    {
        _testRequestFactory = new TestRequestFactory();
        _discoveryServiceMock = new DiscoveryServiceMock();
        _nullGitMetadataProvider = new NullGitMetadataProvider();
        _data = new ArraySegment<byte>(Encoding.UTF8.GetBytes("str"));
    }

    [Theory]
    [InlineData("debugger/v1/input", "application/json")]
    [InlineData("debugger/v1/diagnostics", "multipart/form-data")]
    public async Task SendsDiagnosticsInTheFormatForTheDiscoveredEndpoint(string endpoint, string contentType)
    {
        var settings = new TracerSettings().Manager;
        using var transport = new DebuggerAgentTransport(settings, _discoveryServiceMock, _ => _testRequestFactory);
        var api = new DiagnosticsUploadApi(transport, _nullGitMetadataProvider);
        _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: endpoint, discoverySettings: settings.InitialExporterSettings);

        await api.SendBatchAsync(_data);
        _testRequestFactory.RequestsSent.First().ContentType.Should().StartWith(contentType);
    }

    [Theory]
    [CombinatorialData]
    public async Task UsesOnlyDiscoveryForTheCurrentExporter(bool discoveryFirst)
    {
        const string endpoint = "debugger/v1/diagnostics";
        var settings = new TracerSettings(new DictionaryConfigurationSource(
            new Dictionary<string, string> { ["DD_TRACE_AGENT_URL"] = "http://original:8126" })).Manager;
        var originalExporter = settings.InitialExporterSettings;
        var updatedFactory = new TestRequestFactory();
        ExporterSettings updatedExporter = null;
        using var publisher = settings.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                updatedExporter = exporter;
                if (discoveryFirst)
                {
                    _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: endpoint, discoverySettings: exporter);
                }
            }
        });
        using var transport = new DebuggerAgentTransport(
            settings, _discoveryServiceMock, exporter => ReferenceEquals(exporter, originalExporter) ? _testRequestFactory : updatedFactory);
        var api = new DiagnosticsUploadApi(transport, _nullGitMetadataProvider);
        _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: endpoint, discoverySettings: originalExporter);
        (await api.SendBatchAsync(_data)).Should().BeTrue();
        var originalDiscovery = _discoveryServiceMock.CurrentConfiguration;

        var manual = new ManualInstrumentationConfigurationSource(
            new Dictionary<string, object> { ["DD_TRACE_AGENT_URL"] = new Uri("http://updated:8126") }, useDefaultSources: true);
        settings.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()).Should().BeTrue();
        updatedExporter.Should().NotBeNull();
        var replay = _discoveryServiceMock.Callbacks.Should().ContainSingle().Which;
        replay(originalDiscovery);
        if (!discoveryFirst)
        {
            (await api.SendBatchAsync(_data)).Should().BeFalse();
            updatedFactory.RequestsSent.Should().BeEmpty();
            _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: endpoint, discoverySettings: updatedExporter);
        }

        (await api.SendBatchAsync(_data)).Should().BeTrue();
        _testRequestFactory.RequestsSent.Should().ContainSingle();
        updatedFactory.RequestsSent.Should().ContainSingle();
        transport.Dispose();
        _discoveryServiceMock.Callbacks.Should().BeEmpty();
        (await api.SendBatchAsync(_data)).Should().BeFalse();
    }

    [Fact]
    public async Task UsesCurrentOwnerSettingsForLiveDiagnosticsTags()
    {
        var settings = new TracerSettings(new DictionaryConfigurationSource(
            new Dictionary<string, string>
            {
                ["DD_ENV"] = "rc-debugger-owner",
                ["DD_VERSION"] = "rc-debugger-owner-version"
            })).Manager;
        using var transport = new DebuggerAgentTransport(settings, _discoveryServiceMock, _ => _testRequestFactory);
        var api = new DiagnosticsUploadApi(transport, _nullGitMetadataProvider);
        _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: "debugger/v1/diagnostics", discoverySettings: settings.InitialExporterSettings);

        (await api.SendBatchAsync(_data)).Should().BeTrue();

        var request = _testRequestFactory.RequestsSent.Should().ContainSingle().Which;
        Uri.UnescapeDataString(request.Endpoint.Query).Should().Contain("env:rc-debugger-owner")
           .And.Contain("version:rc-debugger-owner-version");

        var manual = new ManualInstrumentationConfigurationSource(
            new Dictionary<string, object> { ["DD_ENV"] = "rc-debugger-updated", ["DD_VERSION"] = "rc-debugger-updated-version" },
            useDefaultSources: true);
        settings.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()).Should().BeTrue();

        (await api.SendBatchAsync(_data)).Should().BeTrue();

        _testRequestFactory.RequestsSent.Should().HaveCount(2);
        Uri.UnescapeDataString(_testRequestFactory.RequestsSent[1].Endpoint.Query).Should().Contain("env:rc-debugger-updated")
           .And.Contain("version:rc-debugger-updated-version");
    }

    [Fact]
    public async Task KeepsNewAgentDiscoveryWhenStartupReplaysThePreviousAgent()
    {
        const string endpoint = "debugger/v1/diagnostics";
        var timeout = TimeSpan.FromSeconds(10);
        var settings = new TracerSettings(new DictionaryConfigurationSource(
            new Dictionary<string, string> { ["DD_TRACE_AGENT_URL"] = "http://original:8126" })).Manager;
        var originalExporter = settings.InitialExporterSettings;
        _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: endpoint, discoverySettings: originalExporter);
        var originalDiscovery = _discoveryServiceMock.CurrentConfiguration;
        var updatedFactory = new TestRequestFactory();
        var manual = new ManualInstrumentationConfigurationSource(
            new Dictionary<string, object> { ["DD_TRACE_AGENT_URL"] = new Uri("http://updated:8126") }, useDefaultSources: true);
        using var releaseExporterNotification = new ManualResetEventSlim();
        var publishing = false;
        var exporterNotificationReleased = false;
        using var settingsPublisher = settings.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                Volatile.Write(ref publishing, true);
                _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: endpoint, discoverySettings: exporter);
                exporterNotificationReleased = releaseExporterNotification.Wait(timeout);
            }
        });
        var updated = false;
        var publisher = new Thread(() => updated = settings.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()))
        {
            IsBackground = true
        };
        var discovery = new Mock<IDiscoveryService>();
        discovery.SetupGet(service => service.CurrentConfiguration).Returns(() => _discoveryServiceMock.CurrentConfiguration);
        discovery.Setup(service => service.TryRunWithCurrentConfiguration(It.IsAny<Func<AgentConfiguration, bool>>()))
                 .Returns((Func<AgentConfiguration, bool> apply) => _discoveryServiceMock.TryRunWithCurrentConfiguration(apply));
        discovery.Setup(service => service.SubscribeToChanges(It.IsAny<Action<AgentConfiguration>>())).Callback<Action<AgentConfiguration>>(callback =>
        {
            _discoveryServiceMock.SubscribeToChanges(callback);
            publisher.Start();
            try
            {
                // Discovery is either waiting to enter the transport or has returned to the settings publisher.
                SpinWait.SpinUntil(
                    () => Volatile.Read(ref publishing) && (publisher.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    timeout).Should().BeTrue();
                callback(originalDiscovery);
            }
            finally
            {
                releaseExporterNotification.Set();
            }
        });
        discovery.Setup(service => service.RemoveSubscription(It.IsAny<Action<AgentConfiguration>>()))
                 .Callback<Action<AgentConfiguration>>(_discoveryServiceMock.RemoveSubscription);

        using var transport = new DebuggerAgentTransport(
            settings, discovery.Object, exporter => ReferenceEquals(exporter, originalExporter) ? _testRequestFactory : updatedFactory);
        publisher.Join(timeout).Should().BeTrue();
        updated.Should().BeTrue();
        exporterNotificationReleased.Should().BeTrue();

        var api = new DiagnosticsUploadApi(transport, _nullGitMetadataProvider);
        (await api.SendBatchAsync(_data)).Should().BeTrue();
        _testRequestFactory.RequestsSent.Should().BeEmpty();
        updatedFactory.RequestsSent.Should().ContainSingle();
    }
}
