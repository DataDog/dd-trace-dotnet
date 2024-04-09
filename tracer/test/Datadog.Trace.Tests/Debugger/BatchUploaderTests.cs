// <copyright file="BatchUploaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Upload;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class BatchUploaderTests
{
    private const int MaxPayloadSize = 5 * 1024 * 1024;
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string HebrewChars = "אבגדהוזחטי";
    private static readonly Random Random = new Random();

    private readonly MockBatchUploadApi _api;
    private readonly BatchUploader _upload;

    public BatchUploaderTests()
    {
        _api = new MockBatchUploadApi();
        _upload = BatchUploader.Create(_api);
    }

    [Fact]
    public async Task HugePayload_Skipped()
    {
        await _upload.Upload(new[] { GenerateString(MaxPayloadSize) });
        _api.Segments.Should().BeEmpty();
    }

    [Fact]
    public async Task SmallPayloads_BatchInOne()
    {
        await _upload.Upload(new[]
        {
            GenerateString(64),
            GenerateString(64),
            GenerateString(64),
            GenerateString(64),
            GenerateString(64),
        });

        _api.Segments.Count.Should().Be(1);
    }

    [Fact]
    public async Task MediumPayloads_Batched()
    {
        await _upload.Upload(new[]
        {
            GenerateString(1000 * 1024),
            GenerateString(1000 * 1024),
            GenerateString(1000 * 1024),
            GenerateString(1000 * 1024),
            GenerateString(1000 * 1024),
            GenerateString(1000 * 1024),
        });

        _api.Segments.Count.Should().Be(2);
    }

    [Fact]
    public async Task HebrewSerializedHugePayload_Skipped()
    {
        await _upload.Upload(new[]
        {
            GenerateString(1024 * 1024 * 3, true)
        });

        _api.Segments.Count.Should().Be(0);
    }

    [Fact]
    public async Task HebrewSerializedHugePayloadInTheMiddle_MiddleBatchedHugeIgnored()
    {
        await _upload.Upload(new[]
        {
            GenerateString(64),
            GenerateString(1024 * 1024 * 3, true),
            GenerateString(64),
        });

        _api.Segments.Count.Should().Be(1);
    }

    [Fact]
    public async Task HebrewSerializedMediumPayloadExceed_MiddleBatchedHugeIgnored()
    {
        await _upload.Upload(new[]
        {
            GenerateString(512 * 1024, true),
        });

        _api.Segments.Count.Should().Be(0);
    }

    [Fact]
    public async Task CheckSmallOutput()
    {
        await _upload.Upload(new[]
        {
            "Test1",
            "Test2",
        });

        _api.Segments.Count.Should().Be(1);
        Encoding.UTF8.GetString(_api.Segments[0]).Should().Be("[Test1,Test2]");
    }

    [Fact]
    public async Task CheckTwoSmallOutputs()
    {
        await _upload.Upload(new[] { "Test1", "Test2" });
        await _upload.Upload(new[] { "Test3", });

        _api.Segments.Count.Should().Be(2);
        Encoding.UTF8.GetString(_api.Segments[0]).Should().Be("[Test1,Test2]");
        Encoding.UTF8.GetString(_api.Segments[1]).Should().Be("[Test3]");
    }

    [Fact]
    public async Task CheckMediumOutput()
    {
        var str = GenerateString(1023 * 1024);
        await _upload.Upload(new[]
        {
            str,
            str,
            str,
            str,
            str,
            str,
        });

        _api.Segments.Count.Should().Be(2);

        (Encoding.UTF8.GetString(_api.Segments[0]) == $"[{str},{str},{str},{str},{str}]").Should().BeTrue();
        (Encoding.UTF8.GetString(_api.Segments[1]) == $"[{str}]").Should().BeTrue();
    }

    private string GenerateString(int length, bool useHebrew = false)
    {
        return new string(
            Enumerable.Repeat(useHebrew ? HebrewChars : Chars, length)
                      .Select(s => s[Random.Next(s.Length)])
                      .ToArray());
    }

    private class MockBatchUploadApi : IBatchUploadApi
    {
        public List<byte[]> Segments { get; } = new List<byte[]>();

        public Task<bool> SendBatchAsync(ArraySegment<byte> symbols)
        {
            Segments.Add(symbols.ToArray());
            return Task.FromResult(true);
        }
    }
}
