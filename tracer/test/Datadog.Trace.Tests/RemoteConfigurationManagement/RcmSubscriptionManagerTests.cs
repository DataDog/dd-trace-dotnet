// <copyright file="RcmSubscriptionManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RcmSubscriptionManagerTests
{
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

        var subscriptionManager = new RcmSubscriptionManager();
        subscriptionManager.SetCapability(1UL << capabilityIndex, true);
        subscriptionManager.GetCapabilities().Should().BeEquivalentTo(expectedBytes);
    }

    [Fact]
    public async Task SendRequest_FirstRequest_HasEmptyCachedFilesAndConfigStates()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.AsmFeatures));

        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Should().NotBeNull();
        captured.CachedTargetFiles.Should().BeEmpty();
        captured.Client.State.ConfigStates.Should().BeEmpty();
        captured.Client.State.TargetsVersion.Should().Be(0);
        captured.Client.State.BackendClientState.Should().BeNull();
        captured.Client.State.HasError.Should().BeFalse();
        captured.Client.State.Error.Should().BeNull();
        captured.Client.Products.Should().Contain(RcmProducts.AsmFeatures);
    }

    [Fact]
    public async Task SendRequest_FirstRequest_PassesThroughTracerInfo()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.AsmFeatures));

        var tracer = CreateTracer();
        GetRcmRequest captured = null;
        await manager.SendRequest(
            tracer,
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Client.ClientTracer.Should().BeSameAs(tracer);
    }

    [Fact]
    public async Task SendRequest_FirstRequest_IncludesCapabilities()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.AsmFeatures));
        manager.SetCapability(RcmCapabilitiesIndices.AsmActivation, true);

        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Client.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendRequest_FirstRequest_ProductsReflectAllSubscriptions()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.AsmFeatures, RcmProducts.AsmDd));
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.LiveDebugging));

        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Client.Products.Should()
                .BeEquivalentTo(
                     new[] { RcmProducts.AsmFeatures, RcmProducts.AsmDd, RcmProducts.LiveDebugging });
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_NewConfigs_DeliveredToSubscriber()
    {
        var manager = new RcmSubscriptionManager();
        Dictionary<string, List<RemoteConfiguration>> receivedConfigs = null;

        var subscription = new Subscription(
            (configs, removed) =>
            {
                receivedConfigs = configs;
                return configs.SelectMany(c => c.Value)
                              .Select(c => ApplyDetails.FromOk(c.Path.Path))
                              .ToArray();
            },
            RcmProducts.AsmFeatures);
        manager.SubscribeToChanges(subscription);

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1", """{"asm":{"enabled":true}}""");
        var response = CreateSingleProductResponse(new[] { entry });

        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        receivedConfigs.Should().NotBeNull();
        receivedConfigs.Should().ContainKey(RcmProducts.AsmFeatures);
        var config = receivedConfigs[RcmProducts.AsmFeatures].Should().ContainSingle().Which;
        config.Path.Path.Should().Be(entry.Path);
        config.Contents.Should().BeEquivalentTo(entry.Raw);
        config.Length.Should().Be(entry.Raw.Length);
        config.Hashes.Should().ContainKey("sha256").WhoseValue.Should().Be(entry.Hash);
        config.Version.Should().Be(1);
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_MultipleConfigs_AllDelivered()
    {
        var manager = new RcmSubscriptionManager();
        Dictionary<string, List<RemoteConfiguration>> receivedConfigs = null;
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    receivedConfigs = configs;
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        var entry1 = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var entry2 = MakeConfig(RcmProducts.AsmFeatures, "config-2");
        var response = CreateSingleProductResponse(new[] { entry1, entry2 });

        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        receivedConfigs[RcmProducts.AsmFeatures].Should().HaveCount(2);
        receivedConfigs[RcmProducts.AsmFeatures]
           .Select(c => c.Path.Path)
           .Should()
           .BeEquivalentTo(new[] { entry1.Path, entry2.Path });
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_UnchangedHashes_SubscriberNotCalled()
    {
        var manager = new RcmSubscriptionManager();
        int callCount = 0;
        manager.SubscribeToChanges(
            new Subscription(
                (configs, _) =>
                {
                    callCount++;
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response = CreateSingleProductResponse(new[] { entry });

        // First poll: applies config
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));
        callCount.Should().Be(1);

        // Second poll with same hash: subscriber should NOT be called
        var sameResponse = CreateSingleProductResponse(new[] { entry });
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(sameResponse));
        callCount.Should().Be(1, "subscriber should not be called when hashes are unchanged");
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_ChangedHash_ConfigRedelivered()
    {
        var manager = new RcmSubscriptionManager();
        var receivedVersions = new List<long>();
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    foreach (var c in configs.SelectMany(x => x.Value))
                    {
                        receivedVersions.Add(c.Version);
                    }

                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response1 = CreateSingleProductResponse(new[] { entry });
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response1));

        // Second poll with different hash
        var updatedEntry = new ConfigEntry(entry.Path, entry.Raw, "new-hash", 2);
        var response2 = CreateSingleProductResponse(new[] { updatedEntry }, targetsVersion: 2);
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response2));

        receivedVersions.Should().BeEquivalentTo(new long[] { 1, 2 });
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_ConfigRemoved_SubscriberNotifiedOfRemoval()
    {
        var manager = new RcmSubscriptionManager();
        Dictionary<string, List<RemoteConfigurationPath>> receivedRemovals = null;
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    receivedRemovals = removed;
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        var entry1 = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var entry2 = MakeConfig(RcmProducts.AsmFeatures, "config-2");

        // First poll: apply both configs
        var response1 = CreateSingleProductResponse(new[] { entry1, entry2 });
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response1));

        // Second poll: only config-1 remains (config-2 removed)
        var response2 = CreateSingleProductResponse(new[] { entry1 }, targetsVersion: 2);
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response2));

        receivedRemovals.Should().NotBeNull();
        receivedRemovals.Should().ContainKey(RcmProducts.AsmFeatures);
        receivedRemovals[RcmProducts.AsmFeatures]
           .Should()
           .ContainSingle()
           .Which.Path.Should()
           .Be(entry2.Path);
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_RemovedConfig_NotIncludedInSubsequentRequest()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                    configs.SelectMany(c => c.Value)
                           .Select(c => ApplyDetails.FromOk(c.Path.Path))
                           .ToArray(),
                RcmProducts.AsmFeatures));

        var entry1 = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var entry2 = MakeConfig(RcmProducts.AsmFeatures, "config-2");

        // Apply both
        var response1 = CreateSingleProductResponse(new[] { entry1, entry2 });
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response1));

        // Remove config-2
        var response2 = CreateSingleProductResponse(new[] { entry1 }, targetsVersion: 2);
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response2));

        // Third request: only config-1 should be cached
        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.CachedTargetFiles.Should()
                .ContainSingle()
                .Which.Path.Should()
                .Be(entry1.Path);
        captured.Client.State.ConfigStates.Should()
                .ContainSingle()
                .Which.Id.Should()
                .Be("config-1");
    }

    [Fact]
    public async Task SendRequest_AfterProcessing_RequestContainsCachedTargetFiles()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                    configs.SelectMany(c => c.Value)
                           .Select(c => ApplyDetails.FromOk(c.Path.Path))
                           .ToArray(),
                RcmProducts.AsmFeatures));

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response = CreateSingleProductResponse(new[] { entry });
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        // Next request should include cached file
        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        var cached = captured.CachedTargetFiles.Should().ContainSingle().Which;
        cached.Path.Should().Be(entry.Path);
        cached.Length.Should().Be(entry.Raw.Length);
        cached.Hashes.Should()
              .ContainSingle()
              .Which.Should()
              .Match<RcmCachedTargetFileHash>(h => h.Algorithm == "sha256" && h.Hash == entry.Hash);
    }

    [Theory]
    [InlineData(ApplyStates.ACKNOWLEDGED, null)]
    [InlineData(ApplyStates.ERROR, "something went wrong")]
    public async Task SendRequest_AfterProcessing_ConfigState_ReflectsApplyResult(ulong expectedState, string expectedError)
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                    configs.SelectMany(c => c.Value)
                           .Select(c =>
                            {
                                if (expectedState == ApplyStates.ERROR)
                                {
                                    return ApplyDetails.FromError(c.Path.Path, expectedError);
                                }

                                return ApplyDetails.FromOk(c.Path.Path);
                            })
                           .ToArray(),
                RcmProducts.AsmFeatures));

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response = CreateSingleProductResponse(new[] { entry });
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        var state = captured.Client.State.ConfigStates.Should().ContainSingle().Which;
        state.Id.Should().Be("config-1");
        state.Product.Should().Be(RcmProducts.AsmFeatures);
        state.ApplyState.Should().Be(expectedState);
        state.ApplyError.Should().Be(expectedError);
    }

    [Fact]
    public async Task SendRequest_AfterProcessing_RequestContainsTargetsVersionAndBackendState()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                    configs.SelectMany(c => c.Value)
                           .Select(c => ApplyDetails.FromOk(c.Path.Path))
                           .ToArray(),
                RcmProducts.AsmFeatures));

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response = CreateSingleProductResponse(
            new[] { entry },
            targetsVersion: 42,
            backendClientState: "my-opaque-state");
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Client.State.TargetsVersion.Should().Be(42);
        captured.Client.State.BackendClientState.Should().Be("my-opaque-state");
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_UnrequestedProduct_ConfigIgnored()
    {
        var manager = new RcmSubscriptionManager();
        Dictionary<string, List<RemoteConfiguration>> receivedConfigs = null;
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    receivedConfigs = configs;
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        // Response contains configs for both ASM_FEATURES (subscribed) and LIVE_DEBUGGING (not subscribed)
        var asmEntry = MakeConfig(RcmProducts.AsmFeatures, "asm-config");
        var diEntry = MakeConfig(RcmProducts.LiveDebugging, "di-config");

        var targets = new Dictionary<string, Target>
        {
            {
                asmEntry.Path,
                new Target
                {
                    Length = asmEntry.Raw.Length,
                    Hashes = new Dictionary<string, string> { { "sha256", asmEntry.Hash } },
                    Custom = new TargetCustom { V = 1 }
                }
            },
            {
                diEntry.Path,
                new Target
                {
                    Length = diEntry.Raw.Length,
                    Hashes = new Dictionary<string, string> { { "sha256", diEntry.Hash } },
                    Custom = new TargetCustom { V = 1 }
                }
            }
        };

        var targetFiles = new List<RcmFile>
        {
            new RcmFile { Path = asmEntry.Path, Raw = asmEntry.Raw },
            new RcmFile { Path = diEntry.Path, Raw = diEntry.Raw }
        };

        var clientConfigs = new List<string> { asmEntry.Path, diEntry.Path };
        var response = CreateResponse(targets, targetFiles, clientConfigs);

        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        // Only ASM_FEATURES config should be delivered
        receivedConfigs.Should().ContainKey(RcmProducts.AsmFeatures);
        receivedConfigs.Should().NotContainKey(RcmProducts.LiveDebugging);

        // Subsequent request should only cache the subscribed config
        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.CachedTargetFiles.Should()
                .ContainSingle()
                .Which.Path.Should()
                .Be(asmEntry.Path);
    }

    [Fact]
    public async Task SendRequest_ProcessResponse_MultipleSubscriptions_EachReceivesOwnProducts()
    {
        var manager = new RcmSubscriptionManager();
        Dictionary<string, List<RemoteConfiguration>> asmConfigs = null;
        Dictionary<string, List<RemoteConfiguration>> diConfigs = null;

        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    asmConfigs = configs;
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    diConfigs = configs;
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.LiveDebugging));

        var asmEntry = MakeConfig(RcmProducts.AsmFeatures, "asm-config");
        var diEntry = MakeConfig(RcmProducts.LiveDebugging, "di-config");

        var targets = new Dictionary<string, Target>
        {
            {
                asmEntry.Path,
                new Target
                {
                    Length = asmEntry.Raw.Length,
                    Hashes = new Dictionary<string, string> { { "sha256", asmEntry.Hash } },
                    Custom = new TargetCustom { V = 1 }
                }
            },
            {
                diEntry.Path,
                new Target
                {
                    Length = diEntry.Raw.Length,
                    Hashes = new Dictionary<string, string> { { "sha256", diEntry.Hash } },
                    Custom = new TargetCustom { V = 1 }
                }
            }
        };

        var targetFiles = new List<RcmFile>
        {
            new RcmFile { Path = asmEntry.Path, Raw = asmEntry.Raw },
            new RcmFile { Path = diEntry.Path, Raw = diEntry.Raw }
        };

        var clientConfigs = new List<string> { asmEntry.Path, diEntry.Path };
        var response = CreateResponse(targets, targetFiles, clientConfigs);

        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        asmConfigs.Should().ContainKey(RcmProducts.AsmFeatures);
        asmConfigs.Should().NotContainKey(RcmProducts.LiveDebugging);

        diConfigs.Should().ContainKey(RcmProducts.LiveDebugging);
        diConfigs.Should().NotContainKey(RcmProducts.AsmFeatures);
    }

    [Fact]
    public async Task SendRequest_NullResponse_NoProcessing()
    {
        var manager = new RcmSubscriptionManager();
        int callCount = 0;
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    callCount++;
                    return Array.Empty<ApplyDetails>();
                },
                RcmProducts.AsmFeatures));

        await manager.SendRequest(CreateTracer(), _ => Task.FromResult<GetRcmResponse>(null));

        callCount.Should().Be(0);
    }

    [Fact]
    public async Task SendRequest_ResponseWithNullTargets_NoProcessing()
    {
        var manager = new RcmSubscriptionManager();
        int callCount = 0;
        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    callCount++;
                    return Array.Empty<ApplyDetails>();
                },
                RcmProducts.AsmFeatures));

        var response = new GetRcmResponse(); // Targets defaults to null
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        callCount.Should().Be(0);
    }

    [Fact]
    public async Task SendRequest_FullLifecycle_ApplyThenSteadyStateThenUpdateThenRemove()
    {
        var manager = new RcmSubscriptionManager();
        var allReceivedConfigs = new List<Dictionary<string, List<RemoteConfiguration>>>();
        var allReceivedRemovals = new List<Dictionary<string, List<RemoteConfigurationPath>>>();

        manager.SubscribeToChanges(
            new Subscription(
                (configs, removed) =>
                {
                    allReceivedConfigs.Add(new Dictionary<string, List<RemoteConfiguration>>(configs));
                    allReceivedRemovals.Add(removed is null ? null : new Dictionary<string, List<RemoteConfigurationPath>>(removed));
                    return configs.SelectMany(c => c.Value)
                                  .Select(c => ApplyDetails.FromOk(c.Path.Path))
                                  .ToArray();
                },
                RcmProducts.AsmFeatures));

        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");

        // === Step 1: Initial apply ===
        var response1 = CreateSingleProductResponse(
            new[] { entry },
            targetsVersion: 1,
            backendClientState: "state-v1");
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response1));

        allReceivedConfigs.Should().HaveCount(1);
        allReceivedConfigs[0][RcmProducts.AsmFeatures].Should().ContainSingle();

        // Verify request state after step 1
        GetRcmRequest req1 = null;
        await manager.SendRequest(
            CreateTracer(),
            r =>
            {
                req1 = r;
                return Task.FromResult<GetRcmResponse>(null);
            });

        req1.Client.State.TargetsVersion.Should().Be(1);
        req1.Client.State.BackendClientState.Should().Be("state-v1");
        req1.CachedTargetFiles.Should().ContainSingle();
        req1.Client.State.ConfigStates.Should()
            .ContainSingle()
            .Which.ApplyState.Should()
            .Be(ApplyStates.ACKNOWLEDGED);

        // === Step 2: Steady state (same hash) ===
        var response2 = CreateSingleProductResponse(
            new[] { entry },
            targetsVersion: 1,
            backendClientState: "state-v1");
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response2));

        // Subscriber not called again (no new configs or removals)
        allReceivedConfigs.Should().HaveCount(1, "subscriber should not be called when nothing changed");

        // === Step 3: Update (new hash) ===
        var updatedEntry = new ConfigEntry(entry.Path, Encoding.UTF8.GetBytes("""{"asm":{"enabled":false}}"""), "updated-hash", 2);
        var response3 = CreateSingleProductResponse(
            new[] { updatedEntry },
            targetsVersion: 2,
            backendClientState: "state-v2");
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response3));

        allReceivedConfigs.Should().HaveCount(2);
        allReceivedConfigs[1][RcmProducts.AsmFeatures]
           .Should()
           .ContainSingle()
           .Which.Version.Should()
           .Be(2);

        // Verify request state after step 3
        GetRcmRequest req3 = null;
        await manager.SendRequest(
            CreateTracer(),
            r =>
            {
                req3 = r;
                return Task.FromResult<GetRcmResponse>(null);
            });

        req3.Client.State.TargetsVersion.Should().Be(2);
        req3.Client.State.BackendClientState.Should().Be("state-v2");
        req3.CachedTargetFiles.Should()
            .ContainSingle()
            .Which.Hashes.Should()
            .ContainSingle()
            .Which.Hash.Should()
            .Be("updated-hash");

        // === Step 4: Removal (empty response) ===
        var response4 = CreateSingleProductResponse(
            Array.Empty<ConfigEntry>(),
            targetsVersion: 3,
            backendClientState: "state-v3");
        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response4));

        allReceivedConfigs.Should().HaveCount(3);
        allReceivedRemovals.Last().Should().ContainKey(RcmProducts.AsmFeatures);
        allReceivedRemovals.Last()[RcmProducts.AsmFeatures]
                           .Should()
                           .ContainSingle()
                           .Which.Path.Should()
                           .Be(entry.Path);

        // Verify request state after step 4: no cached files
        GetRcmRequest req4 = null;
        await manager.SendRequest(
            CreateTracer(),
            r =>
            {
                req4 = r;
                return Task.FromResult<GetRcmResponse>(null);
            });

        req4.CachedTargetFiles.Should().BeEmpty();
        req4.Client.State.ConfigStates.Should().BeEmpty();
        req4.Client.State.TargetsVersion.Should().Be(3);
        req4.Client.State.BackendClientState.Should().Be("state-v3");
    }

    [Fact]
    public async Task SendRequest_RootVersion_DefaultsToOneAndUpdatesFromLastRootsEntry()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.AsmFeatures));

        // First request: root version should default to 1
        GetRcmRequest captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Client.State.RootVersion.Should().Be(1);

        // Second request: respond with roots containing multiple entries;
        // only the last entry's version should be used
        var response = CreateSingleProductResponse(Array.Empty<ConfigEntry>());
        response.Roots.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"signed":{"version":2}}""")));
        response.Roots.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"signed":{"version":5}}""")));

        await manager.SendRequest(CreateTracer(), _ => Task.FromResult(response));

        // Third request: should use the version from the last roots entry
        captured = null;
        await manager.SendRequest(
            CreateTracer(),
            request =>
            {
                captured = request;
                return Task.FromResult<GetRcmResponse>(null);
            });

        captured.Client.State.RootVersion.Should().Be(5);
    }

    [Fact]
    public async Task BuildRequest_CachesRequest_AndInvalidatesWhenInputsChange()
    {
        var manager = new RcmSubscriptionManager();
        manager.SubscribeToChanges(
            new Subscription(
                (configs, _) =>
                    configs.SelectMany(c => c.Value)
                           .Select(c => ApplyDetails.FromOk(c.Path.Path))
                           .ToArray(),
                RcmProducts.AsmFeatures));

        var tracer = CreateTracer();
        var request = await CaptureRequest(manager, tracer);

        // Always returns the same object (mutated in-place)
        var request2 = await CaptureRequest(manager, tracer);
        request2.Should().BeSameAs(request, "request should always be the same object (mutated in-place)");

        // Different tracer reference → tracer updated on the client
        var tracer2 = CreateTracer();
        await CaptureRequest(manager, tracer2);
        request.Client.ClientTracer.Should().BeSameAs(tracer2, "tracer should be updated when reference changes");

        // Capabilities change → capabilities updated on the client
        var capsBefore = request.Client.Capabilities;
        manager.SetCapability(RcmCapabilitiesIndices.AsmActivation, true);
        await CaptureRequest(manager, tracer2);
        request.Client.Capabilities.Should().NotBeEquivalentTo(capsBefore, "capabilities should be updated when they change");

        // Product keys change (new subscription) → products updated on the client
        var productsBefore = request.Client.Products;
        manager.SubscribeToChanges(new Subscription((_, _) => [], RcmProducts.LiveDebugging));
        await CaptureRequest(manager, tracer2);
        request.Client.Products.Should().NotBeSameAs(productsBefore, "products should be updated when subscriptions change");
        request.Client.Products.Should().Contain(RcmProducts.LiveDebugging);

        // Process a response (changes targets version, backend state, applied configs)
        var entry = MakeConfig(RcmProducts.AsmFeatures, "config-1");
        var response = CreateSingleProductResponse(new[] { entry }, targetsVersion: 5, backendClientState: "state-1");
        await manager.SendRequest(tracer2, _ => Task.FromResult(response));
        await CaptureRequest(manager, tracer2);
        request.Client.State.TargetsVersion.Should().Be(5);
        request.Client.State.BackendClientState.Should().Be("state-1");
        request.CachedTargetFiles.Should().NotBeEmpty();

        // Steady state → values remain the same
        var targetFilesBefore = request.CachedTargetFiles;
        await CaptureRequest(manager, tracer2);
        request.CachedTargetFiles.Should().BeSameAs(targetFilesBefore, "cached target files should not be rebuilt when nothing changed");

        // Config removed → cached target files cleared
        var emptyResponse = CreateSingleProductResponse(Array.Empty<ConfigEntry>(), targetsVersion: 6, backendClientState: "state-2");
        await manager.SendRequest(tracer2, _ => Task.FromResult(emptyResponse));
        await CaptureRequest(manager, tracer2);
        request.CachedTargetFiles.Should().BeEmpty();

        // Poll error → hasError set
        var malformedResponse = new GetRcmResponse();
        malformedResponse.ClientConfigs.Add("datadog/2/ASM_FEATURES/missing/config");
        malformedResponse.Targets = new TufRoot
        {
            Signed = new Signed
            {
                Targets = new Dictionary<string, Target>(),
                Version = 7,
                Custom = new TargetsCustom { OpaqueBackendState = "state-3" }
            }
        };
        await manager.SendRequest(tracer2, _ => Task.FromResult(malformedResponse));
        await CaptureRequest(manager, tracer2);
        request.Client.State.HasError.Should().BeTrue();

        static async Task<GetRcmRequest> CaptureRequest(RcmSubscriptionManager mgr, RcmClientTracer t)
        {
            GetRcmRequest captured = null;
            await mgr.SendRequest(t, req =>
            {
                captured = req;
                return Task.FromResult<GetRcmResponse>(null);
            });
            return captured;
        }
    }

    private static RcmClientTracer CreateTracer() =>
        RcmClientTracer.Create(
            runtimeId: "test-runtime-id",
            tracerVersion: "2.55.0",
            service: "test-service",
            env: "test",
            appVersion: "1.0.0",
            globalTags: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            processTags: null);

    private static GetRcmResponse CreateResponse(
        Dictionary<string, Target> targets,
        List<RcmFile> targetFiles,
        List<string> clientConfigs,
        long targetsVersion = 1,
        string backendClientState = "test-backend-state")
    {
        var response = new GetRcmResponse();

        foreach (var file in targetFiles)
        {
            response.TargetFiles.Add(file);
        }

        foreach (var config in clientConfigs)
        {
            response.ClientConfigs.Add(config);
        }

        response.Targets = new TufRoot
        {
            Signed = new Signed
            {
                Targets = targets,
                Version = targetsVersion,
                Custom = new TargetsCustom { OpaqueBackendState = backendClientState }
            }
        };

        return response;
    }

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

        return CreateResponse(targets, targetFiles, clientConfigs, targetsVersion, backendClientState);
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
}
