// <copyright file="SymDbRemoteConfigTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymDbRemoteConfigTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetUploadSymbolsReturnsSymDbEnablement(bool uploadSymbols)
    {
        var config = CreateRemoteConfiguration("symDb", new { upload_symbols = uploadSymbols });
        var configsByProduct = new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [config] },
        };

        SymDbRemoteConfig.TryGetUploadSymbols(configsByProduct, out var result).Should().BeTrue();
        result.Should().Be(uploadSymbols);
    }

    [Fact]
    public void TryGetUploadSymbolsIgnoresOtherLiveDebuggingSymbolDbFiles()
    {
        var config = CreateRemoteConfiguration("other", new { upload_symbols = true });
        var configsByProduct = new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [config] },
        };

        SymDbRemoteConfig.TryGetUploadSymbols(configsByProduct, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetUploadSymbolsIgnoresOtherProducts()
    {
        var config = CreateRemoteConfiguration("symDb", new { upload_symbols = true });
        var configsByProduct = new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebugging, [config] },
        };

        SymDbRemoteConfig.TryGetUploadSymbols(configsByProduct, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetUploadSymbolsReturnsFalseForEmptyConfigs()
    {
        var configsByProduct = new Dictionary<string, List<RemoteConfiguration>>();

        SymDbRemoteConfig.TryGetUploadSymbols(configsByProduct, out var result).Should().BeFalse();
        result.Should().BeFalse();
    }

    [Fact]
    public void TryGetUploadSymbolsReturnsFalseForEmptyLiveDebuggingSymbolDbList()
    {
        var configsByProduct = new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [] },
        };

        SymDbRemoteConfig.TryGetUploadSymbols(configsByProduct, out var result).Should().BeFalse();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubscribeForwardsRepeatedEnablementTransitions()
    {
        var values = new List<bool>();
        var subscriptionManager = new RcmSubscriptionManagerMock();
        using var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, values.Add);

        symDbRemoteConfig.Subscribe();

        await subscriptionManager.Update(CreateConfigsByProduct(uploadSymbols: true));
        await subscriptionManager.Update(CreateConfigsByProduct(uploadSymbols: false));
        await subscriptionManager.Update(CreateConfigsByProduct(uploadSymbols: true));

        values.Should().Equal(true, false, true);
    }

    [Fact]
    public async Task SubscribeAcknowledgesSymDbEnablementConfig()
    {
        var subscriptionManager = new RcmSubscriptionManagerMock();
        using var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, _ => { });

        symDbRemoteConfig.Subscribe();

        var config = CreateRemoteConfiguration("symDb", new { upload_symbols = true });
        var result = await subscriptionManager.Update(new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [config] },
        });

        result.Should().ContainSingle()
              .Which.Should().BeEquivalentTo(ApplyDetails.FromOk(config.Path.Path));
    }

    [Fact]
    public void DisposeDuringSubscribeDoesNotLeaveSubscriptionRegistered()
    {
        var subscriptionManager = new RcmSubscriptionManagerMock();
        using var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, _ => { });
        subscriptionManager.OnSubscribe = symDbRemoteConfig.Dispose;

        symDbRemoteConfig.Subscribe();

        subscriptionManager.HasAnySubscription.Should().BeFalse();
    }

    [Fact]
    public void DisposeAfterSubscribeUnregistersSubscription()
    {
        // Documents the invariant DebuggerManager.SubscribeToSymbolDatabaseRemoteConfigurationIfNeeded
        // relies on: a shutdown that disposes after Subscribe() returned must unsubscribe.
        var subscriptionManager = new RcmSubscriptionManagerMock();
        var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, _ => { });

        symDbRemoteConfig.Subscribe();
        subscriptionManager.HasAnySubscription.Should().BeTrue();

        symDbRemoteConfig.Dispose();
        subscriptionManager.HasAnySubscription.Should().BeFalse();
    }

    [Fact]
    public void SubscribeAfterDisposeIsNoOp()
    {
        // Documents the other ordering: if shutdown disposes BEFORE the init thread
        // gets to call Subscribe(), Subscribe must safely no-op so the subscription
        // is never registered.
        var subscriptionManager = new RcmSubscriptionManagerMock();
        var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, _ => { });

        symDbRemoteConfig.Dispose();
        symDbRemoteConfig.Subscribe();

        subscriptionManager.HasAnySubscription.Should().BeFalse();
    }

    [Fact]
    public async Task RemovedSymDbConfigDisablesUploader()
    {
        var values = new List<bool>();
        var subscriptionManager = new RcmSubscriptionManagerMock();
        using var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, values.Add);

        symDbRemoteConfig.Subscribe();

        await subscriptionManager.Update(CreateConfigsByProduct(uploadSymbols: true));

        var removedPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.LiveDebuggingSymbolDb}/symDb/config");
        var result = await subscriptionManager.Update(
            new Dictionary<string, List<RemoteConfiguration>>(),
            new Dictionary<string, List<RemoteConfigurationPath>>
            {
                { RcmProducts.LiveDebuggingSymbolDb, [removedPath] },
            });

        values.Should().Equal(true, false);

        // Removals are not acknowledged.
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovedNonSymDbConfigIsIgnored()
    {
        var values = new List<bool>();
        var subscriptionManager = new RcmSubscriptionManagerMock();
        using var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, values.Add);

        symDbRemoteConfig.Subscribe();

        await subscriptionManager.Update(CreateConfigsByProduct(uploadSymbols: true));

        var removedPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.LiveDebuggingSymbolDb}/other/config");
        await subscriptionManager.Update(
            new Dictionary<string, List<RemoteConfiguration>>(),
            new Dictionary<string, List<RemoteConfigurationPath>>
            {
                { RcmProducts.LiveDebuggingSymbolDb, [removedPath] },
            });

        values.Should().Equal(true);
    }

    [Fact]
    public async Task MalformedPayloadProducesApplyError()
    {
        var values = new List<bool>();
        var subscriptionManager = new RcmSubscriptionManagerMock();
        using var symDbRemoteConfig = new SymDbRemoteConfig(subscriptionManager, values.Add);

        symDbRemoteConfig.Subscribe();

        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.LiveDebuggingSymbolDb}/symDb/config");
        var malformedContent = Encoding.UTF8.GetBytes("{not valid json");
        var malformed = new RemoteConfiguration(path, malformedContent, malformedContent.Length, new Dictionary<string, string>(), version: 1);

        var result = await subscriptionManager.Update(new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [malformed] },
        });

        result.Should().ContainSingle()
              .Which.Should().Match<ApplyDetails>(a => a.Filename == path.Path && a.ApplyState == ApplyStates.ERROR && !string.IsNullOrEmpty(a.Error));

        // The callback should not have been invoked because the payload didn't deserialize.
        values.Should().BeEmpty();
    }

    [Fact]
    public void TryGetUploadSymbolsReportsDeserializationError()
    {
        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.LiveDebuggingSymbolDb}/symDb/config");
        var malformedContent = Encoding.UTF8.GetBytes("{not valid json");
        var malformed = new RemoteConfiguration(path, malformedContent, malformedContent.Length, new Dictionary<string, string>(), version: 1);

        var configsByProduct = new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [malformed] },
        };

        SymDbRemoteConfig.TryGetUploadSymbols(configsByProduct, out _, out var configPath, out var deserializationError)
                        .Should().BeFalse();
        configPath.Should().Be(path.Path);
        deserializationError.Should().NotBeNullOrEmpty();
    }

    private static RemoteConfiguration CreateRemoteConfiguration(string id, object payload)
    {
        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.LiveDebuggingSymbolDb}/{id}/config");
        var content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
        return new RemoteConfiguration(path, content, content.Length, new Dictionary<string, string>(), version: 1);
    }

    private static Dictionary<string, List<RemoteConfiguration>> CreateConfigsByProduct(bool uploadSymbols)
    {
        var config = CreateRemoteConfiguration("symDb", new { upload_symbols = uploadSymbols });
        return new Dictionary<string, List<RemoteConfiguration>>
        {
            { RcmProducts.LiveDebuggingSymbolDb, [config] },
        };
    }

    private class RcmSubscriptionManagerMock : IRcmSubscriptionManager
    {
        private ISubscription? _subscription;

        public Action? OnSubscribe { get; set; }

        public bool HasAnySubscription => _subscription is not null;

        public void SubscribeToChanges(ISubscription subscription)
        {
            _subscription = subscription;
            OnSubscribe?.Invoke();
        }

        public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
        {
            _subscription = newSubscription;
        }

        public void Unsubscribe(ISubscription subscription)
        {
            if (_subscription == subscription)
            {
                _subscription = null;
            }
        }

        public void SetCapability(BigInteger index, bool available)
        {
        }

        public Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> callback)
        {
            return Task.CompletedTask;
        }

        public Task<ApplyDetails[]> Update(Dictionary<string, List<RemoteConfiguration>> configsByProduct)
        {
            return Update(configsByProduct, null);
        }

        public Task<ApplyDetails[]> Update(
            Dictionary<string, List<RemoteConfiguration>> configsByProduct,
            Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct)
        {
            return _subscription?.Invoke(configsByProduct, removedConfigsByProduct) ?? Task.FromResult(Array.Empty<ApplyDetails>());
        }
    }
}
