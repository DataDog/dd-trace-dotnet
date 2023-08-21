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
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymbolUploaderTest
{
    private readonly MockBatchUploadApi _api;
    private readonly ISymbolsUploader _upload;

    public SymbolUploaderTest()
    {
        var discoveryService = new DiscoveryServiceMock();
        _api = new MockBatchUploadApi();
        var settings = new DebuggerSettings(
            new NameValueConfigurationSource(new() { { ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled, "true" }, { ConfigurationKeys.Debugger.SymbolBatchSizeInBytes, "1000000" } }),
            NullConfigurationTelemetry.Instance);
        _upload = SymbolsUploader.Create(_api, discoveryService, settings, ImmutableTracerSettings.FromDefaultSources(), "test");
    }

    [Fact]
    public async Task SizeLimitHasNotReached_OneBatch()
    {
        var (root, classes) = GenerateSymbolString(1);
        var success = await UploadClasses(root, classes);
        Assert.True(success);
        var result = DeserializeRoot(_api.Segments.SelectMany(arr => arr).ToArray());
        Assert.NotNull(result);
        var assembly = result.Scopes[0];
        var classesScope = assembly.Scopes;
        Assert.True(result.Scopes.Count == root.Scopes.Count);
        Assert.True(classesScope.Count == 1);
        Assert.True(!string.IsNullOrEmpty(classesScope[0].Name));
        Assert.True(classesScope[0].ScopeType == SymbolType.Class);
    }

    [Fact]
    public async Task SizeLimitHasReached_MoreThanOneBatch()
    {
        var (root, classes) = GenerateSymbolString(1000);
        var success = await UploadClasses(root, classes);
        Assert.True(success);
        var result = DeserializeRoot(_api.Segments.SelectMany(arr => arr).ToArray());
        Assert.NotNull(result);
        var assembly = result.Scopes[0];
        var classesScope = assembly.Scopes;
        Assert.True(result.Scopes.Count == root.Scopes.Count);
        Assert.True(classesScope.Count == 1000);
        Assert.True(!string.IsNullOrEmpty(classesScope[0].Name));
        Assert.True(classesScope.All(cls => cls.ScopeType == SymbolType.Class));
    }

    private async Task<bool> UploadClasses(Root root, IEnumerable<Trace.Debugger.Symbols.Model.Scope?> classes)
    {
        var uploadClassesMethod = ((SymbolsUploader)_upload).GetType().GetMethod("UploadClasses", BindingFlags.Instance | BindingFlags.NonPublic);
        if (uploadClassesMethod == null)
        {
            return false;
        }

        await ((Task)uploadClassesMethod.Invoke(_upload, new object?[] { root, classes })!).ConfigureAwait(false);
        return true;
    }

    private Root? DeserializeRoot(byte[] json)
    {
        var jsonStr = Encoding.UTF8.GetString(json);
        return JsonConvert.DeserializeObject<Root>(jsonStr);
    }

    private (Root Root, IEnumerable<Trace.Debugger.Symbols.Model.Scope?> Classes) GenerateSymbolString(int numberOfTypes)
    {
        var root = new Root
        {
            Env = "test",
            Language = "dotnet",
            Service = nameof(SymbolUploaderTest),
            Version = "0",
            Scopes = new List<Trace.Debugger.Symbols.Model.Scope> { new() { ScopeType = SymbolType.Assembly, Scopes = new List<Trace.Debugger.Symbols.Model.Scope>() } }
        };

        var scopes = new List<Trace.Debugger.Symbols.Model.Scope?>();
        for (var i = 0; i < numberOfTypes; i++)
        {
            scopes.Add(new Trace.Debugger.Symbols.Model.Scope
            {
                Name = $"type: {i}",
                ScopeType = SymbolType.Class,
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
