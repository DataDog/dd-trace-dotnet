// <copyright file="DataStreamsManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsManagerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(null)]
    public void WhenDisabled_DoesNotInjectContext(bool? dsmSupported)
    {
        var dsm = GetDataStreamManager(false, out var discovery);
        TriggerSupportUpdate(discovery, dsmSupported);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        dsm.InjectPathwayContext(context, headers);

        headers.Values.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void WhenEnabled_RegardlessOfSupport_InjectsContext(bool? isSupported)
    {
        var dsm = GetDataStreamManager(true, out var discovery);
        TriggerSupportUpdate(discovery, isSupported);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        dsm.InjectPathwayContext(context, headers);

        headers.Values.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(null)]
    public void WhenDisabled_DoesNotExtractContext(bool? dsmSupported)
    {
        var enabledDsm = GetDataStreamManager(true, out var enabledDiscovery);
        enabledDiscovery.TriggerChange();
        var disabledDsm = GetDataStreamManager(false, out var discovery);
        TriggerSupportUpdate(discovery, dsmSupported);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        enabledDsm.InjectPathwayContext(context, headers);
        headers.Values.Should().NotBeEmpty();

        disabledDsm.ExtractPathwayContext(headers).Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void WhenEnabled_RegardlessOfSupport_ExtractsContext(bool? isSupported)
    {
        var dsm = GetDataStreamManager(true, out var discovery);
        TriggerSupportUpdate(discovery, isSupported);

        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        dsm.InjectPathwayContext(context, headers);
        headers.Values.Should().NotBeEmpty();

        var extracted = dsm.ExtractPathwayContext(headers);
        extracted.Should().NotBeNull();
        extracted.Value.Hash.Value.Should().Be(context.Hash.Value);
        extracted.Value.PathwayStart.Should().Be(context.PathwayStart);
        extracted.Value.EdgeStart.Should().Be(context.EdgeStart);
    }

    [Fact]
    public void WhenEnabled_AndNoContext_ReturnsNewContext()
    {
        var dsm = GetDataStreamManager(true, out var discovery);
        discovery.TriggerChange();

        var context = dsm.SetCheckpoint(parentPathway: null, new[] { "some-tags" });
        context.Should().NotBeNull();
    }

    [Fact]
    public void WhenEnabled_AndNoContext_HashShouldUseParentHashOfZero()
    {
        var env = "foo";
        var service = "bar";
        var edgeTags = new[] { "some-tags" };
        var dsm = GetDataStreamManager(true, out var discovery);
        discovery.TriggerChange();

        var context = dsm.SetCheckpoint(parentPathway: null, edgeTags);
        context.Should().NotBeNull();

        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null);
        var nodeHash = HashHelper.CalculateNodeHash(baseHash, edgeTags);
        var hash = HashHelper.CalculatePathwayHash(nodeHash, parentHash: new PathwayHash(0));

        context.Value.Hash.Value.Should().Be(hash.Value);
    }

    [Fact]
    public void WhenEnabled_AndHashContext_HashShouldUseParentHash()
    {
        var env = "foo";
        var service = "bar";
        var edgeTags = new[] { "some-tags" };
        var dsm = GetDataStreamManager(true, out var discovery);
        discovery.TriggerChange();
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        var context = dsm.SetCheckpoint(parent, edgeTags);
        context.Should().NotBeNull();

        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag: null);
        var nodeHash = HashHelper.CalculateNodeHash(baseHash, edgeTags);
        var hash = HashHelper.CalculatePathwayHash(nodeHash, parentHash: parent.Hash);

        context.Value.Hash.Value.Should().Be(hash.Value);
    }

    [Fact]
    public void WhenDisabled_SetCheckpoint_ReturnsNull()
    {
        var dsm = GetDataStreamManager(false, out _);
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        var context = dsm.SetCheckpoint(parent, new[] { "some-tags" });
        context.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_DisablesDsm()
    {
        var dsm = GetDataStreamManager(true, out var discovery);
        discovery.TriggerChange();
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        dsm.IsEnabled.Should().BeTrue();

        await dsm.DisposeAsync();
        dsm.IsEnabled.Should().BeFalse();

        var context = dsm.SetCheckpoint(parent, new[] { "some-tags" });
        context.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_IgnoresDiscoveryChanges()
    {
        var dsm = GetDataStreamManager(true, out var discovery);
        discovery.TriggerChange();
        dsm.IsEnabled.Should().BeTrue();

        await dsm.DisposeAsync();
        dsm.IsEnabled.Should().BeFalse();
        discovery.TriggerChange();
        dsm.IsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(null)]
    public async Task WhenDisabled_DoesNotSendPointsToWriter(bool? dsmSupported)
    {
        var dsm = GetDataStreamManager(enabled: false, out var discovery, out var requestFactory);
        TriggerSupportUpdate(discovery, dsmSupported);

        dsm.SetCheckpoint(parentPathway: null, new[] { "edge" });

        await dsm.DisposeAsync();

        requestFactory.RequestsSent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task WhenEnabled_AndSupportUnknownOrUnsupported_DoesNotSendPointsToWriter(bool? dsmSupported)
    {
        var dsm = GetDataStreamManager(enabled: true, out var discovery, out var requestFactory);
        TriggerSupportUpdate(discovery, dsmSupported);

        dsm.SetCheckpoint(parentPathway: null, new[] { "edge" });

        await dsm.DisposeAsync();

        requestFactory.RequestsSent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenEnabled_AndSupported_SendsPointsToWriter()
    {
        var dsm = GetDataStreamManager(enabled: true, out var discovery, out var requestFactory);
        TriggerSupportUpdate(discovery, isSupported: true);

        dsm.SetCheckpoint(parentPathway: null, new[] { "edge" });

        await dsm.DisposeAsync();

        requestFactory.RequestsSent.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task WhenEnabled_AndSupportChanges_SendsPointsToWriter(bool? initialSupport, bool finalSupport)
    {
        var dsm = GetDataStreamManager(true, out var discovery, out var requestFactory);
        TriggerSupportUpdate(discovery, isSupported: initialSupport);

        // preconditions
        dsm.SetCheckpoint(parentPathway: null, new[] { "edge-1" });

        TriggerSupportUpdate(discovery, isSupported: finalSupport); // change in support
        dsm.SetCheckpoint(parentPathway: null, new[] { "edge-2" });

        await dsm.DisposeAsync();
        // can't easily validate that we only sent the second one without extra mocks + interfaces,
        // but the logic is simple enough that it's not a bit issue IMO
        requestFactory.RequestsSent.Should().NotBeEmpty();
    }

    private static void TriggerSupportUpdate(DiscoveryServiceMock discovery, bool? isSupported)
    {
        if (isSupported == true)
        {
            discovery.TriggerChange();
        }
        else if (isSupported == false)
        {
            discovery.TriggerChange(dataStreamsMonitoringEndpoint: null);
        }
    }

    private static DataStreamsManager GetDataStreamManager(bool enabled, out DiscoveryServiceMock discoveryService)
        => GetDataStreamManager(enabled, out discoveryService, out _);

    private static DataStreamsManager GetDataStreamManager(
        bool enabled,
        out DiscoveryServiceMock discoveryService,
        out TestRequestFactory requestFactory)
    {
        discoveryService = new DiscoveryServiceMock();
        requestFactory = new TestRequestFactory();
        var writer = enabled
                         ? DataStreamsWriter.Create("foo", "bar", requestFactory)
                         : null;
        return new DataStreamsManager(
            env: "foo",
            defaultServiceName: "bar",
            writer,
            discoveryService);
    }
}
