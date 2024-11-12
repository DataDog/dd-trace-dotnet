// <copyright file="SymbolUploaderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymbolUploaderTest
{
    private readonly MockBatchUploadApi _api;
    private readonly IDebuggerUploader _uploader;
    private readonly DiscoveryServiceMock _discoveryService;
    private readonly RcmSubscriptionManagerMock _enablementService;

    public SymbolUploaderTest()
    {
        _discoveryService = new DiscoveryServiceMock();
        _enablementService = new RcmSubscriptionManagerMock();
        _api = new MockBatchUploadApi();
        var settings = new DebuggerSettings(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true" }, { ConfigurationKeys.Debugger.SymbolDatabaseBatchSizeInBytes, "10000" } }),
            NullConfigurationTelemetry.Instance);
        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true");
        _uploader = SymbolsUploader.Create(_api, _discoveryService, _enablementService, settings, ImmutableTracerSettings.FromDefaultSources(), "test");
    }

    [Fact]
    public async Task SizeLimitHasNotReached_OneBatch()
    {
        var (root, classes) = GenerateSymbolString(1);
        var success = await UploadClasses(root, classes);
        Assert.True(success);
        var result = DeserializeRoot(_api.Segments.SelectMany(arr => arr).ToArray())[0];
        Assert.NotNull(result);
        Assert.NotNull(result.Scopes);
        Assert.NotNull(root.Scopes);
        var assembly = result.Scopes[0];
        var classesScope = assembly.Scopes;
        Assert.True(result.Scopes.Count == root.Scopes.Count);
        Assert.True(classesScope?.Length == 1);
        Assert.True(!string.IsNullOrEmpty(classesScope[0].Name));
        Assert.True(classesScope[0].ScopeType == ScopeType.Class);
    }

    [Fact]
    public async Task SizeLimitHasReached_MoreThanOneBatch()
    {
        var (root, classes) = GenerateSymbolString(1000);
        var success = await UploadClasses(root, classes);
        Assert.True(success);
        var result = DeserializeRoot(_api.Segments.SelectMany(arr => arr).ToArray());
        Assert.NotNull(result);
        foreach (var root1 in result)
        {
            Assert.NotNull(root1);
            var assembly = root1.Scopes?[0];
            Assert.NotNull(assembly);
            var classesScope = assembly.Value.Scopes;
            Assert.NotNull(root1.Scopes);
            Assert.NotNull(root.Scopes);
            Assert.NotNull(classesScope);
            Assert.True(root1.Scopes.Count == root.Scopes.Count);
            Assert.True(!string.IsNullOrEmpty(classesScope[0].Name));
            Assert.True(classesScope.All(cls => cls.ScopeType == ScopeType.Class));
        }
    }

    private void WaitForDiscoveryService()
    {
        _discoveryService.TriggerChange(symbolDbEndpoint: "1");
    }

    private void WaitForEnablement()
    {
        var symDbEnablement = new SymDbEnablement() { UploadSymbols = true };
        using var stream = new MemoryStream();
        using var streamReader = new StreamWriter(stream);
        using var jsonWriter = new JsonTextWriter(streamReader);
        JsonSerializer.CreateDefault().Serialize(jsonWriter, symDbEnablement);
        var content = stream.ToArray();
        _enablementService.Update(new Dictionary<string, List<RemoteConfiguration>> { { RcmProducts.LiveDebuggingSymbolDb, new List<RemoteConfiguration> { new(null, content, 1, null, 1) } } }, new());
    }

    private async Task<bool> UploadClasses(Root root, IEnumerable<Trace.Debugger.Symbols.Model.Scope?> classes)
    {
        var uploadClassesMethod = ((SymbolsUploader)_uploader).GetType().GetMethod("UploadClasses", BindingFlags.Instance | BindingFlags.NonPublic);
        if (uploadClassesMethod == null)
        {
            return false;
        }

        await ((Task)uploadClassesMethod.Invoke(_uploader, new object?[] { root, classes })!).ConfigureAwait(false);
        return true;
    }

    private Root?[] DeserializeRoot(byte[] json)
    {
        const string startOfRoot = "{\"service\":";
        var jsonStr = Encoding.UTF8.GetString(json);
        return jsonStr.
               Split(new[] { startOfRoot }, StringSplitOptions.RemoveEmptyEntries).
               Select(str => startOfRoot + str).
               Select(JsonConvert.DeserializeObject<Root>).ToArray();
    }

    private (Root Root, IEnumerable<Trace.Debugger.Symbols.Model.Scope?> Classes) GenerateSymbolString(int numberOfTypes)
    {
        var root = new Root
        {
            Env = "test",
            Language = "dotnet",
            Service = nameof(SymbolUploaderTest),
            Version = "0",
            Scopes = new List<Trace.Debugger.Symbols.Model.Scope> { new() { ScopeType = ScopeType.Assembly, Scopes = null } }
        };

        var scopes = new List<Trace.Debugger.Symbols.Model.Scope?>();
        for (var i = 0; i < numberOfTypes; i++)
        {
            scopes.Add(new Trace.Debugger.Symbols.Model.Scope
            {
                Name = $"type: {i}",
                ScopeType = ScopeType.Class,
            });
        }

        return (root, scopes);
    }

    private class MockBatchUploadApi : IBatchUploadApi
    {
        public List<byte[]> Segments { get; } = new();

        public Task<bool> SendBatchAsync(ArraySegment<byte> symbols)
        {
            Segments.Add(symbols.ToArray());
            return Task.FromResult(true);
        }
    }

    private class RcmSubscriptionManagerMock : IRcmSubscriptionManager
    {
        private readonly List<ISubscription> _subscriptions = new();

        public bool HasAnySubscription { get; }

        public ICollection<string> ProductKeys { get; } = null!;

        public void SubscribeToChanges(ISubscription subscription)
        {
            _subscriptions.Add(subscription);
        }

        public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
        {
            _subscriptions.Remove(oldSubscription);
            _subscriptions.Add(newSubscription);
        }

        public void Unsubscribe(ISubscription subscription)
        {
            _subscriptions.Remove(subscription);
        }

        public List<ApplyDetails> Update(Dictionary<string, List<RemoteConfiguration>> configByProducts, Dictionary<string, List<RemoteConfigurationPath>> removedConfigsByProduct)
        {
            foreach (var subscription in _subscriptions)
            {
                var configByProduct = configByProducts.Where(c => subscription.ProductKeys.Contains(c.Key))
                                                      .ToDictionary(c => c.Key, c => c.Value);

                if (configByProduct.Count == 0 && removedConfigsByProduct?.Count == 0)
                {
                    continue;
                }

                subscription.Invoke(configByProduct, removedConfigsByProduct);
            }

            return Enumerable.Empty<ApplyDetails>().ToList();
        }

        public void SetCapability(BigInteger index, bool available)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCapabilities()
        {
            throw new NotImplementedException();
        }

        public Task SendRequest(RcmClientTracer rcmTracer, Func<GetRcmRequest, Task<GetRcmResponse?>> callback)
        {
            throw new NotImplementedException();
        }

        public GetRcmRequest BuildRequest(RcmClientTracer rcmTracer, string? lastPollError)
        {
            throw new NotImplementedException();
        }

        public void ProcessResponse(GetRcmResponse response)
        {
            throw new NotImplementedException();
        }
    }
}
