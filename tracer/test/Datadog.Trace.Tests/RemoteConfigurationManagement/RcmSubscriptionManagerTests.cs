// <copyright file="RcmSubscriptionManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RcmSubscriptionManagerTests
{
    private static readonly RcmClientTracer Tracer = CreateTracer("service");
    private static readonly RemoteConfigEndpoint Endpoint = new("http://localhost:8126/", TimeSpan.FromSeconds(1));

    [Fact]
    public void Poll_SharesOneClient_RebindingItForOtherTargets()
    {
        var manager = new RcmSubscriptionManager();
        var client = new FakeRemoteConfigClient();
        var created = 0;
        string[] services = ["extra"];

        manager.Poll(Factory, Tracer, Endpoint, services).Should().BeTrue();
        manager.Poll(Factory, Tracer, Endpoint, services).Should().BeTrue();

        created.Should().Be(1);
        client.Rebinds.Should().BeEmpty();
        client.ExtraServices.Should().ContainSingle().Which.Should().BeSameAs(services);

        // e.g. by the remote configuration manager of a tracer replaced by Tracer.Configure
        var otherTracer = CreateTracer("other-service");
        var otherEndpoint = new RemoteConfigEndpoint("http://localhost:8127/", TimeSpan.FromSeconds(1));
        manager.Poll(Factory, otherTracer, Endpoint, services);
        manager.Poll(Factory, otherTracer, otherEndpoint, services);

        created.Should().Be(1);
        client.Rebinds.Should().Equal((otherTracer, null), (otherTracer, otherEndpoint));

        IRemoteConfigClient Factory(string clientId, RcmClientTracer tracer, RemoteConfigEndpoint endpoint)
        {
            created++;
            clientId.Should().Be(manager.ClientId);
            tracer.Should().BeSameAs(Tracer);
            endpoint.Should().BeSameAs(Endpoint);
            return client;
        }
    }

    [Fact]
    public void Poll_ClientCreationFails_ReturnsFalse()
    {
        var manager = new RcmSubscriptionManager();

        manager.Poll((_, _, _) => throw new DllNotFoundException(), Tracer, Endpoint, []).Should().BeFalse();
    }

    [Fact]
    public async Task Poll_ConcurrentPolls_DispatchOneAtATime()
    {
        var manager = new RcmSubscriptionManager();
        var client = new FakeRemoteConfigClient(new RemoteConfigurationChanges([CreateConfig(RcmProducts.Asm, "asm")], []));
        var active = 0;
        var overlapped = false;
        manager.SubscribeToChanges(new Subscription(
            (_, _) =>
            {
                overlapped |= Interlocked.Increment(ref active) > 1;
                Thread.Sleep(1);
                Interlocked.Decrement(ref active);
                return [];
            },
            RcmProducts.Asm));

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 20; i++)
            {
                Poll(manager, client);
            }
        })));

        overlapped.Should().BeFalse();
    }

    [Fact]
    public void Cancel_CancelsTheClient()
    {
        var manager = new RcmSubscriptionManager();
        var client = new FakeRemoteConfigClient();

        manager.Cancel();
        Poll(manager, client);
        manager.Cancel();

        client.Cancelled.Should().BeTrue();
    }

    [Fact]
    public void Poll_PushesProductsAndCapabilities_OnlyWhenChanged()
    {
        var manager = new RcmSubscriptionManager();
        var client = new FakeRemoteConfigClient();
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.AsmFeatures));
        manager.SetCapability(RcmCapabilitiesIndices.AsmActivation, true);

        Poll(manager, client);
        Poll(manager, client);

        client.ProductUpdates.Should().ContainSingle();
        client.ProductUpdates[0].Products.Should().BeEquivalentTo(RcmProducts.AsmFeatures);
        client.ProductUpdates[0].Capabilities.Should().Be(RcmCapabilitiesIndices.AsmActivation);

        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.LiveDebugging));
        manager.SetCapability(RcmCapabilitiesIndices.AsmIpBlocking, true);
        Poll(manager, client);

        client.ProductUpdates.Should().HaveCount(2);
        client.ProductUpdates[1].Products.Should().BeEquivalentTo(RcmProducts.AsmFeatures, RcmProducts.LiveDebugging);
        client.ProductUpdates[1].Capabilities.Should().Be(RcmCapabilitiesIndices.AsmActivation | RcmCapabilitiesIndices.AsmIpBlocking);
    }

    [Fact]
    public void Poll_UpdatedConfigs_DeliveredToTheSubscriptionsOfTheirProduct()
    {
        var manager = new RcmSubscriptionManager();
        var asmReceived = new List<RemoteConfiguration>();
        var debuggerReceived = new List<RemoteConfiguration>();
        manager.SubscribeToChanges(new Subscription((configs, _) => Received(configs, asmReceived), RcmProducts.Asm, RcmProducts.AsmDd));
        manager.SubscribeToChanges(new Subscription((configs, _) => Received(configs, debuggerReceived), RcmProducts.LiveDebugging));

        var asm = CreateConfig(RcmProducts.Asm, "asm");
        var asmDd = CreateConfig(RcmProducts.AsmDd, "asm-dd");
        var debugger = CreateConfig(RcmProducts.LiveDebugging, "probe");
        var client = new FakeRemoteConfigClient(new RemoteConfigurationChanges([asm, asmDd, debugger], []));

        Poll(manager, client);

        asmReceived.Should().BeEquivalentTo([asm, asmDd]);
        debuggerReceived.Should().BeEquivalentTo([debugger]);

        static ApplyDetails[] Received(Dictionary<string, List<RemoteConfiguration>> configs, List<RemoteConfiguration> received)
        {
            received.AddRange(configs.SelectMany(c => c.Value));
            return [];
        }
    }

    [Fact]
    public void Poll_RemovedConfigs_DeliveredToAllSubscriptions()
    {
        var manager = new RcmSubscriptionManager();
        var asmRemoved = new List<RemoteConfigurationPath>();
        var debuggerRemoved = new List<RemoteConfigurationPath>();
        manager.SubscribeToChanges(new Subscription((_, removed) => Removed(removed, asmRemoved), RcmProducts.Asm));
        manager.SubscribeToChanges(new Subscription((_, removed) => Removed(removed, debuggerRemoved), RcmProducts.LiveDebugging));

        var path = RemoteConfigurationPath.FromPath("datadog/2/ASM/asm/config");
        Poll(manager, new FakeRemoteConfigClient(new RemoteConfigurationChanges([], [path])));

        asmRemoved.Should().BeEquivalentTo([path]);
        debuggerRemoved.Should().BeEquivalentTo([path]);

        static ApplyDetails[] Removed(Dictionary<string, List<RemoteConfigurationPath>> removed, List<RemoteConfigurationPath> received)
        {
            received.AddRange(removed.SelectMany(c => c.Value));
            return [];
        }
    }

    [Fact]
    public void Poll_NoChanges_SubscriptionsNotCalled()
    {
        var manager = new RcmSubscriptionManager();
        var called = false;
        manager.SubscribeToChanges(new Subscription(
            (_, _) =>
            {
                called = true;
                return [];
            },
            RcmProducts.Asm));

        Poll(manager, new FakeRemoteConfigClient());

        called.Should().BeFalse();
    }

    [Fact]
    public void Poll_FetchFails_SubscriptionsNotCalled()
    {
        var manager = new RcmSubscriptionManager();
        var called = false;
        manager.SubscribeToChanges(new Subscription(
            (_, _) =>
            {
                called = true;
                return [];
            },
            RcmProducts.Asm));

        Poll(manager, new FakeRemoteConfigClient(fetchException: new Exception("agent unreachable")));

        called.Should().BeFalse();
    }

    [Fact]
    public void Poll_ReportsApplyStates()
    {
        var manager = new RcmSubscriptionManager();
        var acknowledged = CreateConfig(RcmProducts.Asm, "ok");
        var failed = CreateConfig(RcmProducts.Asm, "failed");
        var unacknowledged = CreateConfig(RcmProducts.Asm, "unacknowledged");
        manager.SubscribeToChanges(new Subscription(
            (_, _) =>
            [
                ApplyDetails.FromOk(acknowledged.Path.Path),
                ApplyDetails.FromError(failed.Path.Path, "invalid"),
                new ApplyDetails(unacknowledged.Path.Path),
            ],
            RcmProducts.Asm));
        var client = new FakeRemoteConfigClient(new RemoteConfigurationChanges([acknowledged, failed, unacknowledged], []));

        Poll(manager, client);

        client.ApplyStates.Should().BeEquivalentTo(
            [
                (acknowledged.Path.Path, (ulong)ApplyStates.ACKNOWLEDGED, (string)null),
                (failed.Path.Path, (ulong)ApplyStates.ERROR, "invalid"),
            ]);
    }

    [Fact]
    public async Task Poll_SupportsAsynchronousSubscriptions()
    {
        var manager = new RcmSubscriptionManager();
        var config = CreateConfig(RcmProducts.TracerFlareInitiated, "flare");
        manager.SubscribeToChanges(new Subscription(
            async (configs, _) =>
            {
                await Task.Yield();
                return [ApplyDetails.FromOk(configs.Single().Value.Single().Path.Path)];
            },
            RcmProducts.TracerFlareInitiated));
        var client = new FakeRemoteConfigClient(new RemoteConfigurationChanges([config], []));

        await Task.Run(() => Poll(manager, client));

        client.ApplyStates.Should().ContainSingle().Which.Path.Should().Be(config.Path.Path);
    }

    private static void Poll(RcmSubscriptionManager manager, IRemoteConfigClient client)
        => manager.Poll((_, _, _) => client, Tracer, Endpoint, []).Should().BeTrue();

    private static RcmClientTracer CreateTracer(string service)
        => RcmClientTracer.Create("runtime-id", "1.2.3", service, "env", null, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()), null);

    private static RemoteConfiguration CreateConfig(string product, string id)
        => new(RemoteConfigurationPath.FromPath($"datadog/2/{product}/{id}/config"), Encoding.UTF8.GetBytes($"{{\"id\":\"{id}\"}}"), 1);

    private sealed class FakeRemoteConfigClient : IRemoteConfigClient
    {
        private readonly RemoteConfigurationChanges _changes;
        private readonly Exception _fetchException;

        public FakeRemoteConfigClient(RemoteConfigurationChanges changes = null, Exception fetchException = null)
        {
            _changes = changes ?? new RemoteConfigurationChanges([], []);
            _fetchException = fetchException;
        }

        public List<(ICollection<string> Products, BigInteger Capabilities)> ProductUpdates { get; } = [];

        public List<(string Path, ulong State, string Error)> ApplyStates { get; } = [];

        public List<(RcmClientTracer Target, RemoteConfigEndpoint Endpoint)> Rebinds { get; } = [];

        public List<string[]> ExtraServices { get; } = [];

        public bool Cancelled { get; private set; }

        public void SetProducts(ICollection<string> products, BigInteger capabilities) => ProductUpdates.Add((products.ToList(), capabilities));

        public void SetExtraServices(string[] services) => ExtraServices.Add(services);

        public void Rebind(RcmClientTracer target, RemoteConfigEndpoint endpoint) => Rebinds.Add((target, endpoint));

        public RemoteConfigurationChanges Fetch() => _fetchException is null ? _changes : throw _fetchException;

        public void SetApplyState(string path, ulong applyState, string error) => ApplyStates.Add((path, applyState, error));

        public void Cancel() => Cancelled = true;

        public void Dispose()
        {
        }
    }
}
