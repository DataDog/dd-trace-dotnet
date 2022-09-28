// <copyright file="DataStreamsManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.TestHelpers.TransportHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsManagerTests
{
    [Fact]
    public void WhenDisabled_DoesNotInjectContext()
    {
        var dsm = GetDataStreamManager(false);
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        dsm.InjectPathwayContext(context, headers);

        headers.Values.Should().BeEmpty();
    }

    [Fact]
    public void WhenEnabled_InjectsContext()
    {
        var dsm = GetDataStreamManager(true);
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        dsm.InjectPathwayContext(context, headers);

        headers.Values.Should().NotBeEmpty();
    }

    [Fact]
    public void WhenDisabled_DoesNotExtractContext()
    {
        var enabledDsm = GetDataStreamManager(true);
        var disabledDsm = GetDataStreamManager(false);
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

        enabledDsm.InjectPathwayContext(context, headers);
        headers.Values.Should().NotBeEmpty();

        disabledDsm.ExtractPathwayContext(headers).Should().BeNull();
    }

    [Fact]
    public void WhenEnabled_ExtractsContext()
    {
        var dsm = GetDataStreamManager(true);
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(new PathwayHash(123), 1_234_000_000, 5_678_000_000);

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
        var dsm = GetDataStreamManager(true);

        var context = dsm.SetCheckpoint(parentPathway: null, new[] { "some-tags" });
        context.Should().NotBeNull();
    }

    [Fact]
    public void WhenEnabled_AndNoContext_HashShouldUseParentHashOfZero()
    {
        var env = "foo";
        var service = "bar";
        var edgeTags = new[] { "some-tags" };
        var dsm = GetDataStreamManager(true);

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
        var dsm = GetDataStreamManager(true);
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
        var dsm = GetDataStreamManager(false);
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        var context = dsm.SetCheckpoint(parent, new[] { "some-tags" });
        context.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_DisablesDsm()
    {
        var dsm = GetDataStreamManager(true);
        var parent = new PathwayContext(new PathwayHash(123), 12340000, 56780000);

        dsm.IsEnabled.Should().BeTrue();

        await dsm.DisposeAsync();
        dsm.IsEnabled.Should().BeFalse();

        var context = dsm.SetCheckpoint(parent, new[] { "some-tags" });
        context.Should().BeNull();
    }

    private static DataStreamsManager GetDataStreamManager(bool enabled)
        => new DataStreamsManager(
            enabled,
            env: "foo",
            defaultServiceName: "bar",
            new TestRequestFactory());
}
