// <copyright file="AspNetCoreEndpoints.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
#pragma warning disable CS0162 // Unreachable code detected

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity;

public abstract class AspNetCoreEndpoints : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private readonly AspNetCoreTestFixture _fixture;

    private readonly bool _collectionEnabled;
    private readonly int? _messageLimit;

    protected AspNetCoreEndpoints(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string sampleName, bool enableEndpointsCollection, int? limit = null)
        : base(sampleName, outputHelper, "/shutdown", testName: $"ApiSecurity.{sampleName}.{(enableEndpointsCollection ? "CollectionOn" : "CollectionOff")}{(limit.HasValue ? "." + limit.Value : string.Empty)}")
    {
        _fixture = fixture;
        _fixture.SetOutput(outputHelper);

        SetEnvironmentVariable(ConfigurationKeys.AppSec.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionEnabled, enableEndpointsCollection.ToString());

        _collectionEnabled = enableEndpointsCollection;
        _messageLimit = limit;

        if (limit.HasValue)
        {
            SetEnvironmentVariable(ConfigurationKeys.AppSec.ApiSecurityEndpointCollectionMessageLimit, limit.Value.ToString());
        }

        // Heartbeat internal set to a low value to get the telemetry data faster in the tests
        SetEnvironmentVariable(ConfigurationKeys.Telemetry.HeartbeatIntervalSeconds, "1");
    }

    internal ICollection<AppEndpointData> Endpoints { get; private set; }

    [SkippableFact]
    public virtual async Task TestEndpointsCollection()
    {
        await TryStartApp();

        var agent = _fixture.Agent;
        await agent.WaitForLatestTelemetryAsync(x => ((TelemetryData)x).IsRequestType(TelemetryRequestTypes.AppEndpoints));

        var allData = agent.Telemetry.Cast<TelemetryData>().ToArray();
        var telemetryData = allData.Where(x => x.IsRequestType(TelemetryRequestTypes.AppEndpoints)).ToArray().FirstOrDefault();

        // If testing with collection disabled, we should not have any telemetry data
        if (!_collectionEnabled)
        {
            telemetryData.Should().BeNull();
            return;
        }

        telemetryData.Should().NotBeNull();

        var endpoints = telemetryData.TryGetPayload<AppEndpointsPayload>(TelemetryRequestTypes.AppEndpoints);
        endpoints.Should().NotBeNull();

        endpoints.Endpoints.Should().NotBeEmpty();

        // If a limit is set, we should have at most that number of endpoints
        if (_messageLimit.HasValue)
        {
            endpoints.Endpoints.Count.Should().BeLessOrEqualTo(_messageLimit.Value);
        }

        Endpoints = endpoints.Endpoints;
    }

    public override void Dispose()
    {
        base.Dispose();
        _fixture.SetOutput(null);
    }

    private async Task TryStartApp()
    {
        await _fixture.TryStartApp(this, true, useTelemetry: true);
        SetHttpPort(_fixture.HttpPort);
    }
}
#endif
