// <copyright file="SymbolUploaderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class SymbolUploaderTest
{
    private const int SizeLimit = 1;
    private readonly MockBatchUploadApi _api;
    private readonly SymbolsUploader _upload;

    public SymbolUploaderTest()
    {
        _api = new MockBatchUploadApi();
        _upload = SymbolsUploader.Create("test", "0", nameof(SymbolUploaderTest), new SymbolExtractor(), _api, SizeLimit);
    }

    [Fact]
    public async Task SizeLimitHasNotReached_OneBatch()
    {
        var (root, classes) = GenerateSymbolString(1);
        await _upload.UploadClasses(root, classes);
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
        await _upload.UploadClasses(root, classes);
        var result = DeserializeRoot(_api.Segments.SelectMany(arr => arr).ToArray());
        Assert.NotNull(result);
        var assembly = result.Scopes[0];
        var classesScope = assembly.Scopes;
        Assert.True(result.Scopes.Count == root.Scopes.Count);
        Assert.True(classesScope.Count == 1000);
        Assert.True(!string.IsNullOrEmpty(classesScope[0].Name));
        Assert.True(classesScope.All(cls => cls.ScopeType == SymbolType.Class));
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
        for (int i = 0; i < numberOfTypes; i++)
        {
            scopes.Add(new Datadog.Trace.Debugger.Symbols.Model.Scope
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
