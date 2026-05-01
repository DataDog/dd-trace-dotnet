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

        public bool HasAnySubscription => _subscription is not null;

        public void SubscribeToChanges(ISubscription subscription)
        {
            _subscription = subscription;
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

        public Task Update(Dictionary<string, List<RemoteConfiguration>> configsByProduct)
        {
            return _subscription?.Invoke(configsByProduct, null) ?? Task.CompletedTask;
        }
    }
}
