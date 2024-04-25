// <copyright file="TelemetryDataBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.DTOs;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryDataBuilderTests
{
    private readonly ApplicationTelemetryData _application;
    private readonly HostTelemetryData _host;
    private readonly string _namingSchemaVersion = "1";

    public TelemetryDataBuilderTests()
    {
        _application = new ApplicationTelemetryData(
            serviceName: "Test Service",
            env: "integration-ci",
            serviceVersion: "1.0.0",
            tracerVersion: TracerConstants.AssemblyVersion,
            languageName: "dotnet",
            languageVersion: FrameworkDescription.Instance.ProductVersion,
            runtimeName: FrameworkDescription.Instance.Name,
            runtimeVersion: FrameworkDescription.Instance.ProductVersion,
            commitSha: "testCommitSha",
            repositoryUrl: "testRepositoryUrl");
        _host = new HostTelemetryData("MY_MACHINE", "Windows", "arm64");
    }

    [Fact]
    public void WhenHasApplicationAndHostData_GeneratesAppClosingTelemetry()
    {
        var builder = new TelemetryDataBuilder();

        var input = new TelemetryInput(null, null, null, null, null, sendAppStarted: false);
        var result = builder.BuildTelemetryData(_application, _host, input, _namingSchemaVersion, sendAppClosing: true);

        result.Should().NotBeNull();
        result.Application.Should().Be(_application);
        result.SeqId.Should().Be(1);
        result.Payload.Should().BeNull();
    }

    [Fact]
    public void WhenHasApplicationAndHostData_GeneratesHeartbeatTelemetry()
    {
        var builder = new TelemetryDataBuilder();

        var result = builder.BuildHeartbeatData(_application, _host, _namingSchemaVersion);

        result.Should().NotBeNull();
        result.Application.Should().Be(_application);
        result.SeqId.Should().Be(1);
        result.Payload.Should().BeNull();
    }

    [Fact]
    public void WhenHasApplicationAndHostData_GeneratesExtendedHeartbeatTelemetry()
    {
        var builder = new TelemetryDataBuilder();

        var result = builder.BuildExtendedHeartbeatData(_application, _host, null, null, null, _namingSchemaVersion);

        result.Should().NotBeNull();
        result.Application.Should().Be(_application);
        result.SeqId.Should().Be(1);
        result.Payload.Should().BeOfType<AppExtendedHeartbeatPayload>();
    }

    [Fact]
    public void ShouldGenerateIncrementingIds()
    {
        var builder = new TelemetryDataBuilder();

        var input = new TelemetryInput(null, null, null, null, null, sendAppStarted: true);
        var data = builder.BuildTelemetryData(_application, _host, input, _namingSchemaVersion, sendAppClosing: false);
        data.SeqId.Should().Be(1);

        data = builder.BuildTelemetryData(_application, _host, input, _namingSchemaVersion, sendAppClosing: false);
        data.SeqId.Should().Be(2);

        var heartbeatData = builder.BuildHeartbeatData(_application, _host, _namingSchemaVersion);
        heartbeatData.Should().NotBeNull();
        heartbeatData.SeqId.Should().Be(3);
    }

    [Fact]
    public void WhenHasApplicationAndHostData_GeneratesLogTelemetry()
    {
        var builder = new TelemetryDataBuilder();
        var logs = new List<LogMessageData>
            {
                new("This is my debug log", TelemetryLogLevel.DEBUG, DateTimeOffset.UtcNow),
                new("This is my warn log", TelemetryLogLevel.WARN, DateTimeOffset.UtcNow),
                new("This is my error log", TelemetryLogLevel.ERROR, DateTimeOffset.UtcNow),
            };

        var result = builder.BuildLogsTelemetryData(_application, _host, logs, _namingSchemaVersion);

        result.Should().NotBeNull();
        result.Application.Should().Be(_application);
        result.SeqId.Should().Be(1);
        result.Payload.Should()
              .NotBeNull()
              .And.BeOfType<LogsPayload>()
              .Which.Logs.Should()
              .BeSameAs(logs);
    }

    [Theory]
    [MemberData(nameof(TestData.Data), MemberType = typeof(TestData))]
    public void GeneratesExpectedRequestType(
        bool hasConfig,
        bool hasDeps,
        bool hasIntegrations,
        bool hasMetrics,
        bool hasDistributions,
        bool hasProducts,
        bool hasSentAppStarted,
        bool hasSendAppClosing,
        string[] expectedRequests)
    {
        var dependencies = hasDeps ? new List<DependencyTelemetryData> { new("name") } : null;
        var config = hasConfig ? Array.Empty<ConfigurationKeyValue>() : null;
        var integrations = hasIntegrations ? new List<IntegrationTelemetryData>() : null;
        var metrics = hasMetrics ? new List<MetricData>() : null;
        var distributions = hasDistributions ? new List<DistributionMetricData>() : null;
        var products = hasProducts ? new ProductsData() : null;
        var input = new TelemetryInput(config, dependencies, integrations, new MetricResults(metrics, distributions), products, sendAppStarted: !hasSentAppStarted);
        var builder = new TelemetryDataBuilder();

        var result = builder.BuildTelemetryData(_application, _host, in input, _namingSchemaVersion, sendAppClosing: hasSendAppClosing);

        result.Should().NotBeNull();
        var actualRequestTypes = result.Payload is MessageBatchPayload batch
                                     ? batch.Select(x => x.RequestType).ToArray()
                                     : new[] { result.RequestType };
        actualRequestTypes.Should().BeEquivalentTo(expectedRequests);

        using var scope = new AssertionScope();

        result.Application.Should().Be(_application);
        result.ApiVersion.Should().Be(TelemetryConstants.ApiVersionV2);
        result.RuntimeId.Should().Be(Tracer.RuntimeId);
        result.TracerTime.Should().BeInRange(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if (result.Payload is MessageBatchPayload messageBatch)
        {
            result.RequestType.Should().Be(TelemetryRequestTypes.MessageBatch);
            foreach (var batchData in messageBatch)
            {
                var payload = batchData.Payload;
                var requestType = batchData.RequestType;
                if (payload is AppDependenciesLoadedPayload depsPayload)
                {
                    requestType.Should().Be(TelemetryRequestTypes.AppDependenciesLoaded);
                    depsPayload.Dependencies.Should().BeSameAs(dependencies);
                }
                else if (payload is AppIntegrationsChangedPayload integrationsPayload)
                {
                    requestType.Should().Be(TelemetryRequestTypes.AppIntegrationsChanged);
                    integrationsPayload.Integrations.Should().BeSameAs(integrations);
                }
                else if (payload is AppProductChangePayload productsPayload)
                {
                    requestType.Should().Be(TelemetryRequestTypes.AppProductChanged);
                    productsPayload.Products.Should().BeSameAs(products);
                }
                else if (payload is AppClientConfigurationChangedPayload configPayload)
                {
                    requestType.Should().Be(TelemetryRequestTypes.AppClientConfigurationChanged);
                    configPayload.Configuration.Should().BeSameAs(config);
                }
                else if (payload is AppStartedPayload appStarted)
                {
                    requestType.Should().Be(TelemetryRequestTypes.AppStarted);
                    appStarted.Configuration.Should().BeSameAs(config);
                    appStarted.Products.Should().BeSameAs(products);
                }
                else if (payload is GenerateMetricsPayload metricsPayload)
                {
                    requestType.Should().Be(TelemetryRequestTypes.GenerateMetrics);
                    metricsPayload.Series.Should().BeSameAs(metrics);
                }
                else if (payload is DistributionsPayload distPayload)
                {
                    requestType.Should().Be(TelemetryRequestTypes.Distributions);
                    distPayload.Series.Should().BeSameAs(distributions);
                }
                else if (requestType == TelemetryRequestTypes.AppHeartbeat)
                {
                    payload.Should().BeNull();
                }
                else if (requestType == TelemetryRequestTypes.AppClosing)
                {
                    payload.Should().BeNull();
                }
                else
                {
                    true.Should().BeFalse($"Unknown payload type {payload} and request type {requestType}");
                }
            }
        }
        else
        {
            result.RequestType.Should().BeOneOf(TelemetryRequestTypes.AppHeartbeat, TelemetryRequestTypes.AppClosing);
            result.Payload.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2_000, 1)]
    [InlineData(2_001, 2)]
    [InlineData(4_000, 2)]
    [InlineData(5_000, 3)]
    public void SplitsDependencies(int numOfDependencies, int numOfMessages)
    {
        var dependencies = Enumerable.Repeat(
            new DependencyTelemetryData("Something"),
            numOfDependencies)
                                     .ToList();
        var input = new TelemetryInput(null, dependencies, null, null, null, sendAppStarted: false);
        var builder = new TelemetryDataBuilder();

        var result = builder.BuildTelemetryData(_application, _host, in input, _namingSchemaVersion, sendAppClosing: false);

        result.Should().NotBeNull();
        var actualRequestTypes = result.Payload is MessageBatchPayload batch
                                     ? batch.Select(x => x.RequestType).ToArray()
                                     : new[] { result.RequestType };
        actualRequestTypes
           .Where(x => x == TelemetryRequestTypes.AppDependenciesLoaded)
           .Should()
           .HaveCount(numOfMessages);
    }

    public class TestData
    {
        // configuration, dependencies, integrations, metrics, distributions, products, hasSentAppStarted, expected request types
        public static IEnumerable<object[]> Data()
        {
            var options = new[] { true, false };
            return from hasDeps in options
                   from hasIntegrations in options
                   from hasConfig in options
                   from hasMetrics in options
                   from hasDistributions in options
                   from hasProducts in options
                   from hasSentAppStarted in options
                   from hasSendAppClosing in options
                   let potentialPayloads = new List<string>()
                   {
                       hasSentAppStarted ? null : TelemetryRequestTypes.AppStarted,
                       hasDeps ? TelemetryRequestTypes.AppDependenciesLoaded : null,
                       hasIntegrations ? TelemetryRequestTypes.AppIntegrationsChanged : null,
                       (hasConfig && hasSentAppStarted) ? TelemetryRequestTypes.AppClientConfigurationChanged : null,
                       (hasProducts && hasSentAppStarted) ? TelemetryRequestTypes.AppProductChanged : null,
                       hasMetrics ? TelemetryRequestTypes.GenerateMetrics : null,
                       hasDistributions ? TelemetryRequestTypes.Distributions : null,
                       hasSendAppClosing ? TelemetryRequestTypes.AppClosing : null,
                   }
                   let heartbeat = new[] { TelemetryRequestTypes.AppHeartbeat }
                   let payloads = potentialPayloads
                                 .Where(x => !string.IsNullOrEmpty(x))
                                  // we only send heartbeat _or_ app-closing
                                 .Concat(hasSendAppClosing || !hasSentAppStarted ? Array.Empty<string>() : heartbeat)
                                 .ToArray()
                   select new object[] { hasConfig, hasDeps, hasIntegrations, hasMetrics, hasDistributions, hasProducts, hasSentAppStarted, hasSendAppClosing, payloads };
        }
    }
}
