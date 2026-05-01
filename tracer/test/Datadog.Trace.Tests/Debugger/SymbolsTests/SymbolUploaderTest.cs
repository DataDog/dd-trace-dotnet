// <copyright file="SymbolUploaderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymbolUploaderTest
{
    private readonly MockBatchUploadApi _api;
    private readonly IDebuggerUploader _uploader;

    public SymbolUploaderTest()
    {
        var discoveryService = new DiscoveryServiceMock();
        _api = new MockBatchUploadApi();
        var settings = new DebuggerSettings(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true" }, { ConfigurationKeys.Debugger.SymbolDatabaseBatchSizeInBytes, "10000" } }),
            NullConfigurationTelemetry.Instance);

        var tracerSettings = new TracerSettings(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Environment, "SymbolUploaderTests" }, { ConfigurationKeys.ServiceVersion, "1" } }));
        EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true");
        _uploader = SymbolsUploader.Create(_api, discoveryService, tracerSettings, settings, () => "test");
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
            var classesScope = assembly!.Value.Scopes;
            Assert.NotNull(root1.Scopes);
            Assert.NotNull(root.Scopes);
            Assert.NotNull(classesScope);
            Assert.True(root1.Scopes.Count == root.Scopes.Count);
            Assert.True(!string.IsNullOrEmpty(classesScope[0].Name));
            Assert.True(classesScope.All(cls => cls.ScopeType == ScopeType.Class));
        }
    }

    [Fact]
    public async Task DoesNotSendTrailingNullBytesWhenReusingPayloadBuffer()
    {
        // Force the uploader to allocate a large internal payload buffer first
        var (bigRoot, bigClasses) = GenerateSymbolString(2000);
        Assert.True(await UploadClasses(bigRoot, bigClasses));

        // Then send a smaller payload which would reuse the same buffer
        _api.Segments.Clear();
        var (smallRoot, smallClasses) = GenerateSymbolString(1);
        Assert.True(await UploadClasses(smallRoot, smallClasses));

        Assert.NotEmpty(_api.Segments);
        foreach (var segment in _api.Segments)
        {
            var json = Encoding.UTF8.GetString(segment);
            Assert.DoesNotContain('\0', json);
        }
    }

    private async Task<bool> UploadClasses(Root root, IEnumerable<Trace.Debugger.Symbols.Model.Scope> classes)
    {
        var uploadClassesMethod = ((SymbolsUploader)_uploader).GetType().GetMethod("UploadClasses", BindingFlags.Instance | BindingFlags.NonPublic);
        if (uploadClassesMethod == null)
        {
            return false;
        }

        await ((Task)uploadClassesMethod.Invoke(_uploader, [root, classes])!).ConfigureAwait(false);
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

    private (Root Root, IEnumerable<Trace.Debugger.Symbols.Model.Scope> Classes) GenerateSymbolString(int numberOfTypes)
    {
        var root = new Root
        {
            Env = "test",
            Language = "dotnet",
            Service = nameof(SymbolUploaderTest),
            Version = "0",
            Scopes = new List<Trace.Debugger.Symbols.Model.Scope> { new() { ScopeType = ScopeType.Assembly, Scopes = null } }
        };

        var scopes = new List<Trace.Debugger.Symbols.Model.Scope>();
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
}
