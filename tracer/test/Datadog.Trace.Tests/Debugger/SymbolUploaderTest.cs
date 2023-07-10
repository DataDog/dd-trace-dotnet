// <copyright file="BatchUploaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class SymbolUploaderTest
{
    private const int SizeLimit = 1000;
    private readonly MockBatchUploadApi _api;
    private readonly SymbolUploader _upload;

    public SymbolUploaderTest()
    {
        _api = new MockBatchUploadApi();
        _upload = SymbolUploader.Create(_api, SizeLimit);
    }

    [Fact]
    public async Task HugePayload_Skipped()
    {
        await _upload.SendSymbol(GenerateSymbolModel(1000));
        _api.Segments.Should().BeEmpty();
    }

    private SymbolModel GenerateSymbolModel(int numberOfTypes)
    {
        var root = new SymbolModel();
        var scopes = new Datadog.Trace.Debugger.Symbols.Scope[numberOfTypes];
        for (int i = 0; i < numberOfTypes; i++)
        {
            scopes[i] = new Datadog.Trace.Debugger.Symbols.Scope()
            {
                Type = $"type: {i}",
                SymbolType = SymbolType.Class,
            };
        }

        root.Scopes = scopes;
        return root;
    }

    private class MockBatchUploadApi : IBatchUploadApi
    {
        public List<byte[]> Segments { get; } = new List<byte[]>();

        public Task<bool> SendBatchAsync(ArraySegment<byte> snapshots)
        {
            Segments.Add(snapshots.ToArray());
            return Task.FromResult(true);
        }
    }
}
