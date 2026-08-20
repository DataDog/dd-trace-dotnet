// <copyright file="TracerManagerFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.LibDatadog.OtelThreadContext;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.PlatformHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.Tests.Util;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer("AWS_LAMBDA_FUNCTION_NAME", "_DD_EXTENSION_PATH")]
public class TracerManagerFactoryTests : IAsyncLifetime
{
    private TracerManager _manager;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _manager?.ShutdownAsync() ?? Task.CompletedTask;

    [Fact]
    public void RemoteConfigIsAvailableByDefault()
    {
        var settings = new TracerSettings();

        settings.IsRemoteConfigurationAvailable.Should().BeTrue();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<RemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<DynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<TracerFlareManager>();
    }

    [Fact]
    public void RemoteConfigIsDisabledInAwsLambda()
    {
        // Lambda.Create() reads environment variables directly, not through TracerSettings
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "something");
        Environment.SetEnvironmentVariable("_DD_EXTENSION_PATH", Path.GetTempFileName());

        // no source needed
        var settings = new TracerSettings();

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    [Theory]
    [PairwiseData]
    public void RemoteConfigIsDisabledInGcp(bool useDeprecatedEnvVars)
    {
        var source = useDeprecatedEnvVars ?
            GcpHelper.CreateMinimalFirstGenCloudRunFunctionsConfiguration("function-name", "project-id") :
            GcpHelper.CreateMinimalCloudRunFunctionsConfiguration("function-target", "k-service");

        var settings = new TracerSettings(source);

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    [Fact]
    public void RemoteConfigIsDisabledInAzureAppServices()
    {
        var source = AzureAppServiceHelper.CreateMinimalAzureAppServiceConfiguration("site-name");
        var settings = new TracerSettings(source);

        settings.IsRemoteConfigurationAvailable.Should().BeFalse();

        _manager = CreateTracerManager(settings);

        _manager.RemoteConfigurationManager.Should().BeOfType<NullRemoteConfigurationManager>();
        _manager.DynamicConfigurationManager.Should().BeOfType<NullDynamicConfigurationManager>();
        _manager.TracerFlareManager.Should().BeOfType<NullTracerFlareManager>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoveryServiceCanBeDisabled(bool enabled)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.AgentFeaturePollingEnabled, enabled.ToString()));
        var settings = new TracerSettings(source);

        settings.AgentFeaturePollingEnabled.Should().Be(enabled);

        var factory = new TracerManagerFactory();
        var discoveryService = factory.GetDiscoveryService(settings, new ServiceRemappingHash(null));

        if (enabled)
        {
            discoveryService.Should().BeOfType<DiscoveryService>();
        }
        else
        {
            discoveryService.Should().BeSameAs(NullDiscoveryService.Instance);
        }
    }

    [Fact]
    public async Task ReusedScopeManagerUpdatesOtelThreadContextPublisher()
    {
        var firstPublisher = new RecordingPublisher(isEnabled: false);
        var secondPublisher = new RecordingPublisher();
        var scopeManager = new AsyncLocalScopeManager(new DisabledContextTracker(), firstPublisher);
        var previousManager = CreateTracerManager(CreateSettings(otelThreadContextEnabled: false), scopeManager);
        var factory = new TracerManagerFactory(_ => secondPublisher);

        _manager = factory.CreateTracerManager(CreateSettings(otelThreadContextEnabled: true), previousManager);
        await previousManager.ShutdownAsync();

        _manager.ScopeManager.Should().BeSameAs(scopeManager);
        var scope = scopeManager.Activate(CreateSpan(), finishOnClose: false);
        scopeManager.Close(scope);

        firstPublisher.Sets.Should().BeEmpty();
        firstPublisher.ResetCount.Should().Be(0);
        secondPublisher.Sets.Should().ContainSingle();
        secondPublisher.ResetCount.Should().Be(1);
    }

    private static TracerManager CreateTracerManager(TracerSettings settings, IScopeManager scopeManager = null)
    {
        return new TracerManagerFactory().CreateTracerManager(
            settings,
            Mock.Of<IAgentWriter>(),
            Mock.Of<ITraceSampler>(),
            scopeManager ?? Mock.Of<IScopeManager>(),
            new TestStatsdManager(Mock.Of<IDogStatsd>()),
            BuildRuntimeMetrics(),
            BuildLogSubmissionManager(),
            Mock.Of<ITelemetryController>(),
            Mock.Of<IDiscoveryService>(),
            new DataStreamsManager(settings, Mock.Of<IDataStreamsWriter>(), Mock.Of<IDiscoveryService>()),
            remoteConfigurationManager: null,
            dynamicConfigurationManager: null,
            tracerFlareManager: null,
            spanEventsManager: null,
            featureFlags: null);

        static DirectLogSubmissionManager BuildLogSubmissionManager()
            => DirectLogSubmissionManager.Create(
                settings: TracerSettings.Create(new()
                {
                    { ConfigurationKeys.Environment, "test" },
                    { ConfigurationKeys.ServiceName, "test" },
                    { ConfigurationKeys.ServiceVersion, "test" },
                }),
                directLogSettings: new TracerSettings().LogSubmissionSettings,
                azureAppServiceSettings: null,
                gitMetadataTagsProvider: Mock.Of<IGitMetadataTagsProvider>());

        static RuntimeMetricsWriter BuildRuntimeMetrics()
            => new(new TestStatsdManager(Mock.Of<IDogStatsd>()), TimeSpan.FromMinutes(1), inAzureAppServiceContext: false, useDiagnosticsApiListener: false, initializeListener: (_, _, _, _) => Mock.Of<IRuntimeMetricsListener>());
    }

    private static IConfigurationSource CreateConfigurationSource(params (string Key, string Value)[] values)
    {
        var config = new NameValueCollection();

        foreach (var (key, value) in values)
        {
            config.Add(key, value);
        }

        return new NameValueConfigurationSource(config);
    }

    private static TracerSettings CreateSettings(bool otelThreadContextEnabled)
    {
        return new TracerSettings(
            CreateConfigurationSource(
                (ConfigurationKeys.OpenTelemetry.OtelThreadContextEnabled, otelThreadContextEnabled.ToString()),
                (ConfigurationKeys.StartupDiagnosticLogEnabled, false.ToString())));
    }

    private static Span CreateSpan()
    {
        var traceContext = new TraceContext(new StubDatadogTracer());
        var spanContext = new SpanContext(
            parent: null,
            traceContext: traceContext,
            serviceName: "service",
            traceId: new TraceId(0x0123456789ABCDEF, 0xFEDCBA9876543210),
            spanId: 123);
        var span = new Span(spanContext, DateTimeOffset.UtcNow);
        traceContext.AddSpan(span);
        return span;
    }

    private sealed class DisabledContextTracker : IContextTracker
    {
        public bool IsEnabled => false;

        public void Set(ulong localRootSpanId, ulong spanId)
        {
        }

        public void SetEndpoint(ulong localRootSpanId, string endpoint)
        {
        }

        public void Reset()
        {
        }
    }

    private sealed class RecordingPublisher : IOtelThreadContextPublisher
    {
        public RecordingPublisher(bool isEnabled = true)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }

        public List<Span> Sets { get; } = [];

        public int ResetCount { get; private set; }

        public void Set(Span span)
        {
            if (IsEnabled)
            {
                Sets.Add(span);
            }
        }

        public void Reset()
        {
            if (IsEnabled)
            {
                ResetCount++;
            }
        }
    }
}
