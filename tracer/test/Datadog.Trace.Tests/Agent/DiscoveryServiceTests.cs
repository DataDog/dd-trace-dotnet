// <copyright file="DiscoveryServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

public class DiscoveryServiceTests
{
    // using shorter retry values for faster tests
    // long recheck to ensure we're not hitting it accidentally
    private const int InitialRetryDelayMs = 50;
    private const int MaxRetryDelayMs = 50;
    private const int RecheckIntervalMs = 300_000;

    [Fact]
    public async Task HandlesFlakyConfiguration()
    {
        using var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x => new FaultyApiRequest(x),
            x => new TestApiRequest(x));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(x => mutex.Set());

        mutex.Wait(30_000).Should().BeTrue("Should raise subscription changes");

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task ReturnsDeserializedConfig()
    {
        AgentConfiguration config = null;
        var clientDropP0s = true;
        var version = "1.26.3";
        var evpProxyEndpoint = "evp_proxy/v4";
        using var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x => new TestApiRequest(x, responseContent: GetConfig(clientDropP0s, version)));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(
            x =>
            {
                config = x;
                mutex.Set();
            });

        mutex.Wait(30_000).Should().BeTrue("Should raise subscription changes");
        config.Should().NotBeNull();
        config.AgentVersion.Should().Be(version);
        config.ConfigurationEndpoint.Should().NotBeNullOrEmpty();
        config.DebuggerEndpoint.Should().NotBeNullOrEmpty();
        config.DiagnosticsEndpoint.Should().NotBeNullOrEmpty();
        config.SymbolDbEndpoint.Should().NotBeNullOrEmpty();
        config.ClientDropP0s.Should().Be(clientDropP0s);
        config.StatsEndpoint.Should().NotBeNullOrEmpty();
        config.DataStreamsMonitoringEndpoint.Should().NotBeNullOrEmpty();
        config.EventPlatformProxyEndpoint.Should().Be(evpProxyEndpoint);
        await ds.DisposeAsync();
    }

    [Fact]
    public async Task CalculatesConfigStateHash()
    {
        var serializedConfig = "{\n\t\"version\": \"7.65.2\",\n\t\"git_commit\": \"0e9956bce2\",\n\t\"endpoints\": [\n\t\t\"/v0.3/traces\",\n\t\t\"/v0.3/services\",\n\t\t\"/v0.4/traces\",\n\t\t\"/v0.4/services\",\n\t\t\"/v0.5/traces\",\n\t\t\"/v0.7/traces\",\n\t\t\"/profiling/v1/input\",\n\t\t\"/telemetry/proxy/\",\n\t\t\"/v0.6/stats\",\n\t\t\"/v0.1/pipeline_stats\",\n\t\t\"/openlineage/api/v1/lineage\",\n\t\t\"/evp_proxy/v1/\",\n\t\t\"/evp_proxy/v2/\",\n\t\t\"/evp_proxy/v3/\",\n\t\t\"/evp_proxy/v4/\",\n\t\t\"/debugger/v1/input\",\n\t\t\"/debugger/v1/diagnostics\",\n\t\t\"/symdb/v1/input\",\n\t\t\"/dogstatsd/v1/proxy\",\n\t\t\"/dogstatsd/v2/proxy\",\n\t\t\"/tracer_flare/v1\",\n\t\t\"/v0.7/config\",\n\t\t\"/config/set\"\n\t],\n\t\"client_drop_p0s\": true,\n\t\"span_meta_structs\": true,\n\t\"long_running_spans\": true,\n\t\"span_events\": true,\n\t\"evp_proxy_allowed_headers\": [\n\t\t\"Content-Type\",\n\t\t\"Accept-Encoding\",\n\t\t\"Content-Encoding\",\n\t\t\"User-Agent\",\n\t\t\"DD-CI-PROVIDER-NAME\"\n\t],\n\t\"config\": {\n\t\t\"default_env\": \"andrew\",\n\t\t\"target_tps\": 10,\n\t\t\"max_eps\": 200,\n\t\t\"receiver_port\": 8126,\n\t\t\"receiver_socket\": \"\",\n\t\t\"connection_limit\": 0,\n\t\t\"receiver_timeout\": 0,\n\t\t\"max_request_bytes\": 26214400,\n\t\t\"statsd_port\": 8125,\n\t\t\"max_memory\": 500000000,\n\t\t\"max_cpu\": 0.5,\n\t\t\"analyzed_spans_by_service\": {},\n\t\t\"obfuscation\": {\n\t\t\t\"elastic_search\": true,\n\t\t\t\"mongo\": true,\n\t\t\t\"sql_exec_plan\": false,\n\t\t\t\"sql_exec_plan_normalize\": false,\n\t\t\t\"http\": {\n\t\t\t\t\"remove_query_string\": false,\n\t\t\t\t\"remove_path_digits\": false\n\t\t\t},\n\t\t\t\"remove_stack_traces\": false,\n\t\t\t\"redis\": {\n\t\t\t\t\"Enabled\": true,\n\t\t\t\t\"RemoveAllArgs\": false\n\t\t\t},\n\t\t\t\"valkey\": {\n\t\t\t\t\"Enabled\": true,\n\t\t\t\t\"RemoveAllArgs\": false\n\t\t\t},\n\t\t\t\"memcached\": {\n\t\t\t\t\"Enabled\": true,\n\t\t\t\t\"KeepCommand\": false\n\t\t\t}\n\t\t}\n\t},\n\t\"peer_tags\": [\n\t\t\"_dd.base_service\",\n\t\t\"active_record.db.vendor\",\n\t\t\"amqp.destination\",\n\t\t\"amqp.exchange\",\n\t\t\"amqp.queue\",\n\t\t\"aws.queue.name\",\n\t\t\"aws.s3.bucket\",\n\t\t\"bucketname\",\n\t\t\"cassandra.keyspace\",\n\t\t\"db.cassandra.contact.points\",\n\t\t\"db.couchbase.seed.nodes\",\n\t\t\"db.hostname\",\n\t\t\"db.instance\",\n\t\t\"db.name\",\n\t\t\"db.namespace\",\n\t\t\"db.system\",\n\t\t\"db.type\",\n\t\t\"dns.hostname\",\n\t\t\"grpc.host\",\n\t\t\"hostname\",\n\t\t\"http.host\",\n\t\t\"http.server_name\",\n\t\t\"messaging.destination\",\n\t\t\"messaging.destination.name\",\n\t\t\"messaging.kafka.bootstrap.servers\",\n\t\t\"messaging.rabbitmq.exchange\",\n\t\t\"messaging.system\",\n\t\t\"mongodb.db\",\n\t\t\"msmq.queue.path\",\n\t\t\"net.peer.name\",\n\t\t\"network.destination.ip\",\n\t\t\"network.destination.name\",\n\t\t\"out.host\",\n\t\t\"peer.hostname\",\n\t\t\"peer.service\",\n\t\t\"queuename\",\n\t\t\"rpc.service\",\n\t\t\"rpc.system\",\n\t\t\"sequel.db.vendor\",\n\t\t\"server.address\",\n\t\t\"streamname\",\n\t\t\"tablename\",\n\t\t\"topicname\"\n\t],\n\t\"span_kinds_stats_computed\": [\n\t\t\"client\",\n\t\t\"producer\",\n\t\t\"server\",\n\t\t\"consumer\"\n\t],\n\t\"obfuscation_version\": 1\n}";
        var expectedHash = "9265333c1d9b94b2022dcc423a686786bacfeb7db425c61fae03b8248e08f819";

        AgentConfiguration config = null;
        using var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x => new TestApiRequest(x, responseContent: serializedConfig));

        await using var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(
            x =>
            {
                config = x;
                mutex.Set();
            });

        mutex.Wait(30_000).Should().BeTrue("Should raise subscription changes");
        config.Should().NotBeNull();
        config.AgentVersion.Should().Be("7.65.2");
        config.ConfigurationEndpoint.Should().Be("v0.7/config");
        config.DebuggerEndpoint.Should().Be("debugger/v1/input");
        config.DiagnosticsEndpoint.Should().Be("debugger/v1/diagnostics");
        config.SymbolDbEndpoint.Should().Be("symdb/v1/input");
        config.ClientDropP0s.Should().Be(true);
        config.StatsEndpoint.Should().Be("v0.6/stats");
        config.DataStreamsMonitoringEndpoint.Should().Be("v0.1/pipeline_stats");
        config.EventPlatformProxyEndpoint.Should().Be("evp_proxy/v4");
        ds.ConfigStateHash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task DoesNotFireInitialCallbackIfInitialConfigNotFetched()
    {
        var notificationFired = false;
        using var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x =>
            {
                mutex.Wait(10_000).Should().BeTrue("Should make request to api");
                return new TestApiRequest(x, responseContent: GetConfig());
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        ds.SubscribeToChanges(x => notificationFired = true);

        await Task.Delay(5_000); // should recheck 5 times in this duration
        notificationFired.Should().BeFalse();
        mutex.Set();

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task FiresInitialCallbackIfInitialConfigAlreadyFetched()
    {
        int notificationCount = 0;
        using var mutex = new ManualResetEventSlim();
        var factory = new TestRequestFactory(
            x =>
            {
                return new TestApiRequest(x, responseContent: GetConfig());
            },
            y => throw new Exception("Should not make a second request"));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);
        // make sure we have config
        ds.SubscribeToChanges(x => mutex.Set());
        mutex.Wait(30_000).Should().BeTrue("Should make request to api");

        ds.SubscribeToChanges(x => Interlocked.Increment(ref notificationCount));
        Volatile.Read(ref notificationCount).Should().Be(1);

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task DoesNotFireCallbackOnRecheckIfNoChangesToConfig()
    {
        int notificationCount = 0;
        using var mutex1 = new ManualResetEventSlim(); // Allows test to control when Request 1 proceeds
        using var mutex2 = new ManualResetEventSlim();
        using var mutex3 = new ManualResetEventSlim();
        var recheckIntervalMs = 1_000; // ms
        var factory = new TestRequestFactory(
            x =>
            {
                // Block Request 1 until test signals to proceed
                // This ensures subscription happens BEFORE Request 1 completes
                mutex1.Wait(10_000).Should().BeTrue("Test should signal Request 1 to proceed");
                return new TestApiRequest(x, responseContent: GetConfig());
            },
            x =>
            {
                mutex2.Set();
                return new TestApiRequest(x, responseContent: GetConfig());
            },
            x =>
            {
                mutex3.Set();
                return new TestApiRequest(x, responseContent: GetConfig());
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);

        // Subscribe BEFORE Request 1 completes
        // This tests the asynchronous notification path in NotifySubscribers
        ds.SubscribeToChanges(x => Interlocked.Increment(ref notificationCount));

        // Now allow Request 1 to proceed and complete
        mutex1.Set();

        // Wait for second request to ensure Request 1 completed
        mutex2.Wait(30_000).Should().BeTrue("Should make second request to api");

        // Wait for third request - by this time all previous requests are fully processed
        mutex3.Wait(30_000).Should().BeTrue("Should make third request to api");

        Volatile.Read(ref notificationCount).Should().Be(1); // Request 1 notification only, no additional callbacks

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task FiresCallbackOnRecheckIfHasChangesToConfig()
    {
        var notificationCount = 0;
        using var mutex1 = new ManualResetEventSlim(); // Allows test to control when Request 1 proceeds
        using var mutex2 = new ManualResetEventSlim();
        using var mutex3 = new ManualResetEventSlim();
        var recheckIntervalMs = 1_000; // ms
        var factory = new TestRequestFactory(
            x =>
            {
                // Block Request 1 until test signals to proceed
                // This ensures subscription happens BEFORE Request 1 completes
                mutex1.Wait(10_000).Should().BeTrue("Test should signal Request 1 to proceed");
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: true));
            },
            x =>
            {
                mutex2.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: false));
            },
            x =>
            {
                mutex3.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: false));
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);

        // Subscribe BEFORE Request 1 completes
        // This tests the asynchronous notification path in NotifySubscribers
        ds.SubscribeToChanges(x => Interlocked.Increment(ref notificationCount));

        // Now allow Request 1 to proceed and complete
        mutex1.Set();

        // Wait for second request to ensure Request 1 completed
        mutex2.Wait(30_000).Should().BeTrue("Should make second request to api");

        // Wait for third request - by this time Request 2 should be fully processed
        mutex3.Wait(30_000).Should().BeTrue("Should make third request to api");

        Volatile.Read(ref notificationCount).Should().Be(2); // Request 1 + Request 2 (config changed)

        await ds.DisposeAsync();
    }

    [Fact]
    public async Task DoesNotFireAfterUnsubscribing()
    {
        var notificationCount = 0;
        using var mutex1 = new ManualResetEventSlim(); // Allows test to control when Request 1 proceeds
        using var mutex2 = new ManualResetEventSlim();
        using var mutex3 = new ManualResetEventSlim();

        var recheckIntervalMs = 1_000; // ms
        var factory = new TestRequestFactory(
            x =>
            {
                // Block Request 1 until test signals to proceed
                // This ensures subscription happens BEFORE Request 1 completes
                mutex1.Wait(10_000).Should().BeTrue("Test should signal Request 1 to proceed");
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: true));
            },
            x =>
            {
                mutex2.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: false));
            },
            x =>
            {
                mutex3.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: false));
            });

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);

        // Subscribe BEFORE Request 1 completes
        // Callback will fire when Request 1 completes, and immediately unsubscribe
        ds.SubscribeToChanges(Callback);

        // Now allow Request 1 to proceed and complete
        mutex1.Set();

        // Wait for additional requests to ensure callback doesn't fire again after unsubscribing
        mutex2.Wait(30_000).Should().BeTrue("Should make second request to api");

        // wait for third request
        mutex3.Wait(30_000).Should().BeTrue("Should make third request to api");

        Volatile.Read(ref notificationCount).Should().Be(1); // callback should only run once

        await ds.DisposeAsync();

        void Callback(AgentConfiguration x)
        {
            Interlocked.Increment(ref notificationCount);
            ds.RemoveSubscription(Callback);
        }
    }

    [Fact]
    public async Task DisposesInATimelyManner()
    {
        using var mutex = new ManualResetEventSlim();

        var factory = new TestRequestFactory(
            x =>
            {
                mutex.Set();
                return new TestApiRequest(x, responseContent: GetConfig(dropP0: true));
            },
            x => new TestApiRequest(x, responseContent: GetConfig(dropP0: false)));

        var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, RecheckIntervalMs);

        // should be inside recheck loop
        mutex.Wait(30_000).Should().BeTrue("Should make request to api");

        var dispose = ds.DisposeAsync();

        var task = await Task.WhenAny(dispose, Task.Delay(5_000));

        task.Should().Be(dispose, "Should dispose in a timely manner but took >5s");
    }

    [Fact]
    public void AgentConfigurationComparesByValue()
    {
        var config1 = new AgentConfiguration(
            configurationEndpoint: "ConfigurationEndpoint",
            debuggerEndpoint: "DebuggerEndpoint",
            debuggerV2Endpoint: "debuggerV2Endpoint",
            diagnosticsEndpoint: "DiagnosticsEndpoint",
            symbolDbEndpoint: "symbolDbEndpoint",
            agentVersion: "AgentVersion",
            statsEndpoint: "StatsEndpoint",
            dataStreamsMonitoringEndpoint: "DataStreamsMonitoringEndpoint",
            eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
            telemetryProxyEndpoint: "telemetryProxyEndpoint",
            tracerFlareEndpoint: "tracerFlareEndpoint",
            clientDropP0: false,
            spanMetaStructs: true,
            spanEvents: true);

        // same config
        var config2 = new AgentConfiguration(
            configurationEndpoint: "ConfigurationEndpoint",
            debuggerEndpoint: "DebuggerEndpoint",
            debuggerV2Endpoint: "debuggerV2Endpoint",
            diagnosticsEndpoint: "DiagnosticsEndpoint",
            symbolDbEndpoint: "symbolDbEndpoint",
            agentVersion: "AgentVersion",
            statsEndpoint: "StatsEndpoint",
            dataStreamsMonitoringEndpoint: "DataStreamsMonitoringEndpoint",
            eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
            telemetryProxyEndpoint: "telemetryProxyEndpoint",
            tracerFlareEndpoint: "tracerFlareEndpoint",
            clientDropP0: false,
            spanMetaStructs: true,
            spanEvents: true);

        // different
        var config3 = new AgentConfiguration(
            configurationEndpoint: "DIFFERENT",
            debuggerEndpoint: "DebuggerEndpoint",
            debuggerV2Endpoint: "debuggerV2Endpoint",
            diagnosticsEndpoint: "DiagnosticsEndpoint",
            symbolDbEndpoint: "symbolDbEndpoint",
            agentVersion: "AgentVersion",
            statsEndpoint: "StatsEndpoint",
            dataStreamsMonitoringEndpoint: "DataStreamsMonitoringEndpoint",
            eventPlatformProxyEndpoint: "eventPlatformProxyEndpoint",
            telemetryProxyEndpoint: "telemetryProxyEndpoint",
            tracerFlareEndpoint: "tracerFlareEndpoint",
            clientDropP0: false,
            spanMetaStructs: true,
            spanEvents: true);

        config1.Equals(config2).Should().BeTrue();
        config1.Equals(config3).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null, 0, true)] // first loop
    [InlineData("abc", null, 0, true)] // no update yet
    [InlineData(null, "123", 10, true)] // recent update, but never polled
    [InlineData("abc", "123", 10, true)] // recent update, but wrong hash
    [InlineData("abc", "abc", 60_000, true)] // same hash, but old
    [InlineData("abc", "abc", 10_000, false)] // recent update, matches
    public async Task RequireRefresh(string originalHash, string agentHash, int timeElapsed, bool refreshRequired)
    {
        var recheckIntervalMs = 30_000;
        var factory = new TestRequestFactory();
        await using var ds = new DiscoveryService(factory, InitialRetryDelayMs, MaxRetryDelayMs, recheckIntervalMs);

        var now = DateTimeOffset.UtcNow;
        ds.SetCurrentConfigStateHash(agentHash);

        ds.RequireRefresh(originalHash, now.AddMilliseconds(timeElapsed)).Should().Be(refreshRequired);
    }

    [Fact]
    [Flaky("This is an inherently flaky test as it relies on time periods")]
    public async Task HandlesFailuresInApiWithBackoff()
    {
        using var mutex = new ManualResetEventSlim(initialState: false, spinCount: 0);
        var factory = new TestRequestFactory(
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest(),
            _ => new ThrowingRequest());

        // These are the default values in the other constructor
        // but setting them explicitly here as it's the behaviour we're testing
        // not the exact values we choose later
        var ds = new DiscoveryService(factory, initialRetryDelayMs: 500, maxRetryDelayMs: 5_000, recheckIntervalMs: 30_000);
        ds.SubscribeToChanges(_ => mutex.Set());

        // wait for 0 + 500 + 1000 + 2000 + 4000 + 5000 ms (+ 2500 buffer).
        // should not be set
        mutex.Wait(15_000);

        await ds.DisposeAsync();
        // add some leeway in case of slowness
        factory.RequestsSent.Count.Should().BeInRange(3, 6, "Should make between 3 and 6 retries in 13s");
    }

    private string GetConfig(bool dropP0 = true, string version = null)
        => JsonConvert.SerializeObject(new MockTracerAgent.AgentConfiguration() { ClientDropP0s = dropP0, AgentVersion = version });

    internal class ThrowingRequest : TestApiRequest
    {
        public ThrowingRequest()
            : base(new Uri("http://localhost"))
        {
        }

        public override async Task<IApiResponse> GetAsync()
        {
            await Task.Yield();
            throw new WebException("Error in GetAsync");
        }

        public override async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            await Task.Yield();
            throw new WebException("Error in PostAsync");
        }
    }
}
