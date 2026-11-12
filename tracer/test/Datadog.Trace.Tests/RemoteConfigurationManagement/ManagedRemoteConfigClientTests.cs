// <copyright file="ManagedRemoteConfigClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

/// <summary>
/// The protocol tests of the managed client, from the tests of the previous RcmSubscriptionManager
/// </summary>
public class ManagedRemoteConfigClientTests
{
    private static readonly RemoteConfigEndpoint Endpoint = new("http://localhost:8126/", TimeSpan.FromSeconds(1));

    private readonly ITestOutputHelper _output;

    public ManagedRemoteConfigClientTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // some values (noted below) can trigger BigInteger.ToByteArray() to an an extra 0x00 byte,
    // so we're testing a few values to make sure the conversion is correct
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    // ...
    [InlineData(6)]
    [InlineData(7)] // 7 is the last index of the first byte
    [InlineData(8)]
    // ...
    [InlineData(14)]
    [InlineData(15)] // 15 is the last index of the second byte
    [InlineData(16)]
    // ...
    [InlineData(22)]
    [InlineData(23)] // 23 is the last index of the third byte
    [InlineData(24)]
    // ...
    [InlineData(30)]
    [InlineData(31)] // 31 is the last index of the fourth byte
    [InlineData(32)]
    public void GetCapabilities(int capabilityIndex)
    {
        var byteCount = (capabilityIndex / 8) + 1;
        var expectedBytes = new byte[byteCount];
        var bits = new BitArray(expectedBytes) { [capabilityIndex] = true };
        bits.CopyTo(expectedBytes, 0);
        Array.Reverse(expectedBytes);

        ManagedRemoteConfigClient.GetCapabilities(1UL << capabilityIndex).Should().BeEquivalentTo(expectedBytes);
    }

    [Fact]
    public void Fetch_FirstRequest_HasEmptyCachedFilesAndConfigStates()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        var captured = CaptureRequest(client, api);

        captured.Should().NotBeNull();
        captured.CachedTargetFiles.Should().BeEmpty();
        captured.Client.State.ConfigStates.Should().BeEmpty();
        captured.Client.State.RootVersion.Should().Be(1);
        captured.Client.State.TargetsVersion.Should().Be(0);
        captured.Client.State.BackendClientState.Should().BeNull();
        captured.Client.State.HasError.Should().BeFalse();
        captured.Client.State.Error.Should().BeNull();
        captured.Client.Products.Should().Contain(RcmProducts.AsmFeatures);
    }

    [Fact]
    public void Fetch_FirstRequest_PassesThroughClientIdTracerInfoAndExtraServices()
    {
        var api = new FakeApi();
        var tracer = CreateTracer();
        var client = new ManagedRemoteConfigClient("client-id", tracer, Endpoint, _ => api);
        string[] extraServices = ["extra-service"];
        client.SetExtraServices(extraServices);

        var captured = CaptureRequest(client, api);

        captured.Client.Id.Should().Be("client-id");
        captured.Client.IsTracer.Should().BeTrue();
        captured.Client.ClientTracer.Should().BeSameAs(tracer);
        captured.Client.ClientTracer.ExtraServices.Should().BeSameAs(extraServices);
    }

    [Fact]
    public void Fetch_FirstRequest_IncludesCapabilities()
    {
        var api = new FakeApi();
        var client = new ManagedRemoteConfigClient("client-id", CreateTracer(), Endpoint, _ => api);
        client.SetProducts([RcmProducts.AsmFeatures], RcmCapabilitiesIndices.AsmActivation);

        var captured = CaptureRequest(client, api);

        captured.Client.Capabilities.Should().Equal(ManagedRemoteConfigClient.GetCapabilities(RcmCapabilitiesIndices.AsmActivation));
    }

    [Fact]
    public void Fetch_FirstRequest_ProductsAreTheSubscribedProducts()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures, RcmProducts.AsmDd, RcmProducts.LiveDebugging);

        var captured = CaptureRequest(client, api);

        captured.Client.Products.Should().BeEquivalentTo(RcmProducts.AsmFeatures, RcmProducts.AsmDd, RcmProducts.LiveDebugging);
    }

    [Fact]
    public void Fetch_NewConfigs_Returned()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1", """{"asm":{"enabled":true}}""");

        var changes = Fetch(client, api, CreateSingleProductResponse([entry]));

        changes.Removed.Should().BeEmpty();
        var config = changes.Updated.Should().ContainSingle().Which;
        config.Path.Path.Should().Be(entry.Path);
        ReadAll(config.Contents).Should().Equal(entry.Raw);
        config.Version.Should().Be(1);
    }

    [Fact]
    public void Fetch_MultipleConfigs_AllReturned()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry1 = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var entry2 = MakeConfig(RcmProducts.AsmFeatures, "config-2");

        var changes = Fetch(client, api, CreateSingleProductResponse([entry1, entry2]));

        changes.Updated.Select(c => c.Path.Path).Should().BeEquivalentTo(entry1.Path, entry2.Path);
    }

    [Fact]
    public void Fetch_UnchangedHashes_NotReturnedAgain()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");

        Fetch(client, api, CreateSingleProductResponse([entry])).Updated.Should().ContainSingle();

        Fetch(client, api, CreateSingleProductResponse([entry])).IsEmpty.Should().BeTrue("configs are not returned again when hashes are unchanged");
    }

    [Fact]
    public void Fetch_HashesComparedCaseInsensitively()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = new ConfigEntry("datadog/2/ASM_FEATURES/config-1/config", Encoding.UTF8.GetBytes("test"), "ABCDEF", 1);

        Fetch(client, api, CreateSingleProductResponse([entry])).Updated.Should().ContainSingle();

        var sameHashLowerCase = new ConfigEntry(entry.Path, entry.Raw, "abcdef", 1);
        Fetch(client, api, CreateSingleProductResponse([sameHashLowerCase])).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Fetch_ChangedHash_ConfigReturnedAgain()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry])).Updated.Should().ContainSingle().Which.Version.Should().Be(1);

        // Second poll with different hash
        var updatedEntry = new ConfigEntry(entry.Path, entry.Raw, "new-hash", 2);
        var changes = Fetch(client, api, CreateSingleProductResponse([updatedEntry], targetsVersion: 2));

        changes.Updated.Should().ContainSingle().Which.Version.Should().Be(2);
        changes.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Fetch_ConfigRemoved_RemovalReturned()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry1 = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var entry2 = MakeConfig(RcmProducts.AsmFeatures, "config-2");

        // First poll: apply both configs
        Fetch(client, api, CreateSingleProductResponse([entry1, entry2]));

        // Second poll: only config-1 remains (config-2 removed)
        var changes = Fetch(client, api, CreateSingleProductResponse([entry1], targetsVersion: 2));

        changes.Updated.Should().BeEmpty();
        changes.Removed.Should().ContainSingle().Which.Path.Should().Be(entry2.Path);
    }

    [Fact]
    public void Fetch_RemovedConfig_NotIncludedInSubsequentRequest()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry1 = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var entry2 = MakeConfig(RcmProducts.AsmFeatures, "config-2");

        // Apply both
        AcknowledgeAll(client, Fetch(client, api, CreateSingleProductResponse([entry1, entry2])));

        // Remove config-2
        Fetch(client, api, CreateSingleProductResponse([entry1], targetsVersion: 2));

        // Third request: only config-1 should be cached
        var captured = CaptureRequest(client, api);

        captured.CachedTargetFiles.Should().ContainSingle().Which.Path.Should().Be(entry1.Path);
        captured.Client.State.ConfigStates.Should().ContainSingle().Which.Id.Should().Be("config-1");
    }

    [Fact]
    public void Fetch_AfterProcessing_RequestContainsCachedTargetFiles()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry]));

        // Next request should include cached file
        var captured = CaptureRequest(client, api);

        var cached = captured.CachedTargetFiles.Should().ContainSingle().Which;
        cached.Path.Should().Be(entry.Path);
        cached.Length.Should().Be(entry.Raw.Length);
        cached.Hashes.Should()
              .ContainSingle()
              .Which.Should()
              .Match<RcmCachedTargetFileHash>(h => h.Algorithm == "sha256" && h.Hash == entry.Hash);
        var state = captured.Client.State.ConfigStates.Should().ContainSingle().Which;
        state.Version.Should().Be(1);
        state.ApplyState.Should().Be(ApplyStates.UNACKNOWLEDGED);
    }

    [Theory]
    [InlineData(ApplyStates.ACKNOWLEDGED, null)]
    [InlineData(ApplyStates.ERROR, "something went wrong")]
    public void Fetch_AfterSetApplyState_ConfigStateReflectsApplyResult(ulong expectedState, string expectedError)
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry]));

        client.SetApplyState(entry.Path, expectedState, expectedError);
        var captured = CaptureRequest(client, api);

        var state = captured.Client.State.ConfigStates.Should().ContainSingle().Which;
        state.Id.Should().Be("config-1");
        state.Product.Should().Be(RcmProducts.AsmFeatures);
        state.ApplyState.Should().Be(expectedState);
        state.ApplyError.Should().Be(expectedError);
    }

    [Fact]
    public void SetApplyState_UnknownPath_Ignored()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        client.SetApplyState("datadog/2/ASM_FEATURES/unknown/config", ApplyStates.ACKNOWLEDGED, null);

        CaptureRequest(client, api).Client.State.ConfigStates.Should().BeEmpty();
    }

    [Fact]
    public void Fetch_AfterProcessing_RequestContainsTargetsVersionAndBackendState()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry], targetsVersion: 42, backendClientState: "my-opaque-state"));

        var captured = CaptureRequest(client, api);

        captured.Client.State.TargetsVersion.Should().Be(42);
        captured.Client.State.BackendClientState.Should().Be("my-opaque-state");
    }

    [Fact]
    public void Fetch_UnrequestedProduct_ConfigIgnored()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        // Response contains configs for both ASM_FEATURES (subscribed) and LIVE_DEBUGGING (not subscribed)
        var asmEntry = MakeConfig(RcmProducts.AsmFeatures, "asm-config");
        var diEntry = MakeConfig(RcmProducts.LiveDebugging, "di-config");
        var changes = Fetch(client, api, CreateSingleProductResponse([asmEntry, diEntry]));

        // Only ASM_FEATURES config should be returned
        changes.Updated.Should().ContainSingle().Which.Path.Path.Should().Be(asmEntry.Path);

        // Subsequent request should only cache the subscribed config
        CaptureRequest(client, api).CachedTargetFiles.Should().ContainSingle().Which.Path.Should().Be(asmEntry.Path);
    }

    [Fact]
    public void Fetch_NullResponse_NoChanges()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        Fetch(client, api, null).IsEmpty.Should().BeTrue();
        api.Requests.Should().Be(1);
    }

    [Fact]
    public void Fetch_ResponseWithNullTargets_NoChanges()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry]));

        // Targets defaults to null: nothing is processed, not even removals
        Fetch(client, api, new GetRcmResponse()).IsEmpty.Should().BeTrue();
        CaptureRequest(client, api).CachedTargetFiles.Should().ContainSingle();
    }

    [Fact]
    public void Fetch_FullLifecycle_ApplyThenSteadyStateThenUpdateThenRemove()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");

        // === Step 1: Initial apply ===
        var changes1 = Fetch(client, api, CreateSingleProductResponse([entry], targetsVersion: 1, backendClientState: "state-v1"));
        changes1.Updated.Should().ContainSingle();
        AcknowledgeAll(client, changes1);

        // Verify request state after step 1
        var req1 = CaptureRequest(client, api);
        req1.Client.State.TargetsVersion.Should().Be(1);
        req1.Client.State.BackendClientState.Should().Be("state-v1");
        req1.CachedTargetFiles.Should().ContainSingle();
        req1.Client.State.ConfigStates.Should().ContainSingle().Which.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);

        // === Step 2: Steady state (same hash) ===
        Fetch(client, api, CreateSingleProductResponse([entry], targetsVersion: 1, backendClientState: "state-v1")).IsEmpty.Should().BeTrue("nothing changed");

        // === Step 3: Update (new hash) ===
        var updatedEntry = new ConfigEntry(entry.Path, Encoding.UTF8.GetBytes("""{"asm":{"enabled":false}}"""), "updated-hash", 2);
        var changes3 = Fetch(client, api, CreateSingleProductResponse([updatedEntry], targetsVersion: 2, backendClientState: "state-v2"));
        changes3.Updated.Should().ContainSingle().Which.Version.Should().Be(2);
        AcknowledgeAll(client, changes3);

        // Verify request state after step 3
        var req3 = CaptureRequest(client, api);
        req3.Client.State.TargetsVersion.Should().Be(2);
        req3.Client.State.BackendClientState.Should().Be("state-v2");
        req3.CachedTargetFiles.Should().ContainSingle().Which.Hashes.Should().ContainSingle().Which.Hash.Should().Be("updated-hash");

        // === Step 4: Removal (empty response) ===
        var changes4 = Fetch(client, api, CreateSingleProductResponse([], targetsVersion: 3, backendClientState: "state-v3"));
        changes4.Updated.Should().BeEmpty();
        changes4.Removed.Should().ContainSingle().Which.Path.Should().Be(entry.Path);

        // Verify request state after step 4: no cached files
        var req4 = CaptureRequest(client, api);
        req4.CachedTargetFiles.Should().BeEmpty();
        req4.Client.State.ConfigStates.Should().BeEmpty();
        req4.Client.State.TargetsVersion.Should().Be(3);
        req4.Client.State.BackendClientState.Should().Be("state-v3");
    }

    [Fact]
    public void Fetch_RootVersion_DefaultsToOneAndUpdatesFromLastRootsEntry()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        // First request: root version should default to 1
        CaptureRequest(client, api).Client.State.RootVersion.Should().Be(1);

        // Second request: respond with roots containing multiple entries;
        // only the last entry's version should be used
        var response = CreateSingleProductResponse([]);
        response.Roots =
        [
            Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"signed":{"version":2}}""")),
            Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"signed":{"version":5}}""")),
        ];
        Fetch(client, api, response);

        // Third request: should use the version from the last roots entry
        CaptureRequest(client, api).Client.State.RootVersion.Should().Be(5);
    }

    [Fact]
    public void Fetch_MissingTargetFile_ErrorReportedInTheNextRequestOnly()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response = CreateSingleProductResponse([entry], targetsVersion: 3);
        response.TargetFiles = [];

        Fetch(client, api, response).IsEmpty.Should().BeTrue();

        var captured = CaptureRequest(client, api);
        captured.Client.State.HasError.Should().BeTrue();
        captured.Client.State.Error.Should().Be($"Missing config {entry.Path} in target files");
        // The response was not processed
        captured.Client.State.TargetsVersion.Should().Be(0);

        // Reported once
        CaptureRequest(client, api).Client.State.HasError.Should().BeFalse();
        api.LastRequest.Client.State.Error.Should().BeNull();
    }

    [Fact]
    public void Fetch_MissingTarget_ErrorReportedInTheNextRequest()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var response = new GetRcmResponse
        {
            ClientConfigs = ["datadog/2/ASM_FEATURES/missing/config"],
            Targets = new TufRoot { Signed = new Signed { Targets = new Dictionary<string, Target>(), Version = 7 } },
        };

        Fetch(client, api, response).IsEmpty.Should().BeTrue();

        var captured = CaptureRequest(client, api);
        captured.Client.State.HasError.Should().BeTrue();
        captured.Client.State.Error.Should().Be("Missing config datadog/2/ASM_FEATURES/missing/config in targets");
    }

    [Fact]
    public void Fetch_DeserializationError_ReportedInTheNextRequest()
    {
        var api = new FakeApi { Exception = new RemoteConfigurationDeserializationException(new JsonReaderException("bad json")) };
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        client.Fetch().IsEmpty.Should().BeTrue();

        api.Exception = null;
        var captured = CaptureRequest(client, api);
        captured.Client.State.HasError.Should().BeTrue();
        captured.Client.State.Error.Should().Be("Error deserializing RCM request: bad json");
    }

    [Fact]
    public void Fetch_TransportError_NotReported()
    {
        var api = new FakeApi { Exception = new IOException("agent unreachable") };
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        client.Fetch().IsEmpty.Should().BeTrue();

        api.Exception = null;
        CaptureRequest(client, api).Client.State.HasError.Should().BeFalse();
    }

    [Fact]
    public void Rebind_SwitchesTheTargetAndTheTransport_KeepingTheState()
    {
        var api = new FakeApi();
        var otherApi = new FakeApi();
        var endpoints = new List<RemoteConfigEndpoint>();
        var otherEndpoint = new RemoteConfigEndpoint("http://localhost:8127/", TimeSpan.FromSeconds(1));
        var client = new ManagedRemoteConfigClient(
            "client-id",
            CreateTracer(),
            Endpoint,
            endpoint =>
            {
                endpoints.Add(endpoint);
                return endpoint == Endpoint ? api : otherApi;
            });
        client.SetProducts([RcmProducts.AsmFeatures], 0);
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry], targetsVersion: 4, backendClientState: "state"));

        var otherTracer = CreateTracer("other-service");
        client.Rebind(otherTracer, null);
        var captured = CaptureRequest(client, api);
        captured.Client.ClientTracer.Should().BeSameAs(otherTracer);

        client.Rebind(otherTracer, otherEndpoint);
        captured = CaptureRequest(client, otherApi);

        endpoints.Should().Equal(Endpoint, otherEndpoint);
        api.Requests.Should().Be(2);
        captured.Client.ClientTracer.Should().BeSameAs(otherTracer);
        captured.Client.State.TargetsVersion.Should().Be(4);
        captured.Client.State.BackendClientState.Should().Be("state");
        captured.CachedTargetFiles.Should().ContainSingle().Which.Path.Should().Be(entry.Path);
    }

    [Fact]
    public void BuildRequest_ReusesTheRequest_AndUpdatesItWhenInputsChange()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        var request = CaptureRequest(client, api);

        // Always returns the same object (mutated in-place)
        CaptureRequest(client, api).Should().BeSameAs(request, "request should always be the same object (mutated in-place)");

        // Different tracer reference → tracer updated on the client
        var tracer2 = CreateTracer();
        client.Rebind(tracer2, null);
        CaptureRequest(client, api);
        request.Client.ClientTracer.Should().BeSameAs(tracer2, "tracer should be updated when reference changes");

        // Capabilities and products change → updated on the client
        var capsBefore = request.Client.Capabilities;
        var productsBefore = request.Client.Products;
        client.SetProducts([RcmProducts.AsmFeatures, RcmProducts.LiveDebugging], RcmCapabilitiesIndices.AsmActivation);
        CaptureRequest(client, api);
        request.Client.Capabilities.Should().NotBeEquivalentTo(capsBefore, "capabilities should be updated when they change");
        request.Client.Products.Should().NotBeSameAs(productsBefore, "products should be updated when subscriptions change");
        request.Client.Products.Should().Contain(RcmProducts.LiveDebugging);

        // Process a response (changes targets version, backend state, applied configs)
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        Fetch(client, api, CreateSingleProductResponse([entry], targetsVersion: 5, backendClientState: "state-1"));
        CaptureRequest(client, api);
        request.Client.State.TargetsVersion.Should().Be(5);
        request.Client.State.BackendClientState.Should().Be("state-1");
        request.CachedTargetFiles.Should().NotBeEmpty();

        // Steady state → values remain the same
        var targetFilesBefore = request.CachedTargetFiles;
        CaptureRequest(client, api);
        request.CachedTargetFiles.Should().BeSameAs(targetFilesBefore, "cached target files should not be rebuilt when nothing changed");

        // Config removed → cached target files cleared
        Fetch(client, api, CreateSingleProductResponse([], targetsVersion: 6, backendClientState: "state-2"));
        CaptureRequest(client, api);
        request.CachedTargetFiles.Should().BeEmpty();
    }

    [Fact]
    public void Cancel_LaterFetchesThrow_WithoutSendingRequests()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);

        client.Cancel();

        client.Invoking(c => c.Fetch()).Should().Throw<OperationCanceledException>();
        api.Requests.Should().Be(0);
    }

    [Fact]
    public void Cancel_DuringAFetch_TheResponseIsIgnored()
    {
        var api = new FakeApi();
        var client = CreateClient(api, RcmProducts.AsmFeatures);
        api.OnRequest = client.Cancel;
        api.Response = CreateSingleProductResponse([MakeConfig(RcmProducts.AsmFeatures, "config-1")]);

        client.Invoking(c => c.Fetch()).Should().Throw<OperationCanceledException>();
        api.Requests.Should().Be(1);
    }

    [Fact]
    public void Fetch_FromTheAgent_SendsTheRequestOfThePreviousClient()
    {
        string response = null;
        foreach (string r in RcmResponses.TargetsTargetFilesClientConfigs())
        {
            response ??= r;
        }

        var expected = JsonConvert.DeserializeObject<GetRcmResponse>(response);
        using var agent = MockTracerAgent.Create(_output, TcpPortProvider.GetOpenPort());
        agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(response);
        var exporter = new ExporterSettings(new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.AgentUri, $"http://127.0.0.1:{agent.Port}" } }));
        var discovery = new DiscoveryServiceMock();
        var tracer = RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", "1.0", new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()), ["entrypoint.name:test"]);

        using var client = ManagedRemoteConfigClient.Create("client-id", tracer, RemoteConfigEndpoint.FromExporterSettings(exporter), discovery);
        client.SetProducts([RcmProducts.Asm, RcmProducts.AsmDd, RcmProducts.AsmFeatures], RcmCapabilitiesIndices.AsmActivation);
        client.SetExtraServices(["extra-service"]);

        // Waits for the discovery service
        client.Fetch().IsEmpty.Should().BeTrue();
        agent.RemoteConfigRequests.Should().BeEmpty();
        discovery.TriggerChange(configurationEndpoint: "v0.7/config");

        var changes = client.Fetch();
        changes.Removed.Should().BeEmpty();
        changes.Updated.Select(c => c.Path.Path).Should().BeEquivalentTo(expected.ClientConfigs);
        foreach (var config in changes.Updated)
        {
            ReadAll(config.Contents).Should().Equal(expected.TargetFiles.Single(f => f.Path == config.Path.Path).Raw);
        }

        client.Fetch().IsEmpty.Should().BeTrue();

        // As before: base64 capabilities, and the opaque backend state as a string
        var json = JObject.Parse(agent.RemoteConfigRequests.Last());
        json["client"]["capabilities"].Type.Should().Be(JTokenType.String);
        Convert.FromBase64String((string)json["client"]["capabilities"]).Should().Equal(ManagedRemoteConfigClient.GetCapabilities(RcmCapabilitiesIndices.AsmActivation));
        json["client"]["state"]["backend_client_state"].Type.Should().Be(JTokenType.String);

        var request = GetRcmRequestExtensions.DeserializeRcmRequest(agent.RemoteConfigRequests.Last());
        request.Matches(expected).Should().BeTrue();
        request.Client.Id.Should().Be("client-id");
        request.Client.State.TargetsVersion.Should().Be(expected.Targets.Signed.Version);
        request.Client.ClientTracer.Service.Should().Be("service");
        request.Client.ClientTracer.ExtraServices.Should().Equal("extra-service");
        request.Client.ClientTracer.ProcessTags.Should().BeEquivalentTo("entrypoint.name:test");
        request.CachedTargetFiles.Select(f => f.Path).Should().BeEquivalentTo(expected.ClientConfigs);
    }

    private static ManagedRemoteConfigClient CreateClient(FakeApi api, params string[] products)
    {
        var client = new ManagedRemoteConfigClient("client-id", CreateTracer(), Endpoint, _ => api);
        client.SetProducts(products, 0);
        return client;
    }

    private static RemoteConfigurationChanges Fetch(ManagedRemoteConfigClient client, FakeApi api, GetRcmResponse response)
    {
        api.Response = response;
        return client.Fetch();
    }

    private static GetRcmRequest CaptureRequest(ManagedRemoteConfigClient client, FakeApi api)
    {
        Fetch(client, api, null).IsEmpty.Should().BeTrue();
        return api.LastRequest;
    }

    // As the subscriptions do, see RcmSubscriptionManager
    private static void AcknowledgeAll(ManagedRemoteConfigClient client, RemoteConfigurationChanges changes)
    {
        foreach (var config in changes.Updated)
        {
            client.SetApplyState(config.Path.Path, ApplyStates.ACKNOWLEDGED, null);
        }
    }

    private static byte[] ReadAll(RemoteConfigurationContent contents)
    {
        using var stream = contents.OpenStream();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static RcmClientTracer CreateTracer(string service = "test-service") =>
        RcmClientTracer.Create(
            runtimeId: "test-runtime-id",
            tracerVersion: "2.55.0",
            service: service,
            env: "test",
            appVersion: "1.0.0",
            globalTags: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            processTags: null);

    private static GetRcmResponse CreateSingleProductResponse(
        ConfigEntry[] configs,
        long targetsVersion = 1,
        string backendClientState = "test-backend-state")
    {
        var targets = new Dictionary<string, Target>();
        var targetFiles = new List<RcmFile>();
        var clientConfigs = new List<string>();

        foreach (var entry in configs)
        {
            targets[entry.Path] = new Target
            {
                Length = entry.Raw.Length,
                Hashes = new Dictionary<string, string> { { "sha256", entry.Hash } },
                Custom = new TargetCustom { V = entry.Version }
            };

            targetFiles.Add(new RcmFile { Path = entry.Path, Raw = entry.Raw });
            clientConfigs.Add(entry.Path);
        }

        return new GetRcmResponse
        {
            TargetFiles = targetFiles,
            ClientConfigs = clientConfigs,
            Targets = new TufRoot
            {
                Signed = new Signed
                {
                    Targets = targets,
                    Version = targetsVersion,
                    Custom = new TargetsCustom { OpaqueBackendState = backendClientState }
                }
            }
        };
    }

    private static ConfigEntry MakeConfig(string product, string id, string content = "test")
    {
        var path = $"datadog/2/{product}/{id}/config";
        var raw = Encoding.UTF8.GetBytes(content);
        var hash = $"sha256-{id}";
        return new ConfigEntry(path, raw, hash, 1);
    }

    private sealed class ConfigEntry
    {
        public ConfigEntry(string path, byte[] raw, string hash, long version)
        {
            Path = path;
            Raw = raw;
            Hash = hash;
            Version = version;
        }

        public string Path { get; }

        public byte[] Raw { get; }

        public string Hash { get; }

        public long Version { get; }
    }

    private sealed class FakeApi : IRemoteConfigurationApi
    {
        public GetRcmResponse Response { get; set; }

        public Exception Exception { get; set; }

        public Action OnRequest { get; set; }

        public GetRcmRequest LastRequest { get; private set; }

        public int Requests { get; private set; }

        public Task<GetRcmResponse> GetConfigs(GetRcmRequest request)
        {
            Requests++;
            LastRequest = request;
            OnRequest?.Invoke();
            return Exception is null ? Task.FromResult(Response) : Task.FromException<GetRcmResponse>(Exception);
        }
    }
}
