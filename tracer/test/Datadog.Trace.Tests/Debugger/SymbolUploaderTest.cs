// <copyright file="SymbolUploaderTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class SymbolUploaderTest
{
    private const int SizeLimit = 500;
    private readonly MockBatchUploadApi _api;
    private readonly SymbolUploader _upload;

    public SymbolUploaderTest()
    {
        _api = new MockBatchUploadApi();
        _upload = SymbolUploader.Create(_api, SizeLimit);
    }

    [Fact]
    public async Task SizeLimitHasNotReached_DoNotSend()
    {
        await _upload.SendSymbol(GenerateSymbolModel(1));
        _api.Segments.Should().BeEmpty();
    }

    [Fact]
    public async Task SizeLimitHasReached_Send()
    {
        await _upload.SendSymbol(GenerateSymbolModel(5));
        _api.Segments.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SizeLimitHasReachedAfterTwoUploads_Send()
    {
        await _upload.SendSymbol(GenerateSymbolModel(1));
        _api.Segments.Should().BeEmpty();
        await _upload.SendSymbol(GenerateSymbolModel(4));
        _api.Segments.Should().NotBeNullOrEmpty();
    }

    private Root GenerateSymbolModel(int numberOfTypes)
    {
        var root = new Root();
        var scopes = new Trace.Debugger.Symbols.Model.Scope[numberOfTypes];
        for (int i = 0; i < numberOfTypes; i++)
        {
            scopes[i] = new Datadog.Trace.Debugger.Symbols.Model.Scope
            {
                Type = $"type: {i}",
                ScopeType = SymbolType.Class,
            };
        }

        root.Scopes = scopes;
        return root;
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
