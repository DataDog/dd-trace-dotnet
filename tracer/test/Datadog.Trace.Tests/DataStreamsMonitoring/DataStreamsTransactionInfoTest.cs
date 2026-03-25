// <copyright file="DataStreamsTransactionInfoTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

[Collection(nameof(DataStreamsTransactionCacheTestCollection))]
public class DataStreamsTransactionInfoTest
{
    [Fact]
    public void TransactionInfoSerializesCorrectly()
    {
        DataStreamsTransactionInfo.ClearCache();
        var transaction = new DataStreamsTransactionInfo("1", 1, "1");
        transaction.TimestampNs.Should().Be(1);
        transaction.GetBytes().Should().BeEquivalentTo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 49 });
    }

    [Fact]
    public void TransactionInfoCacheSerializesCorrectly()
    {
        DataStreamsTransactionInfo.ClearCache();
        _ = new DataStreamsTransactionInfo("1", 1, "1");
        var cacheBytes = DataStreamsTransactionInfo.GetCacheBytes();
        cacheBytes.Should().BeEquivalentTo(new byte[] { 1, 1, 49 });
    }

    [Fact]
    public void GetBytes_ReturnsSameBytesOnSubsequentCalls()
    {
        var transaction = new DataStreamsTransactionInfo("tx-abc-123", 9_876_543_210L, "some-checkpoint");
        transaction.GetBytes().Should().BeEquivalentTo(transaction.GetBytes());
    }

    [Fact]
    public void WriteTo_WritesSameBytesAsGetBytes()
    {
        var transaction = new DataStreamsTransactionInfo("tx-id", 42L, "checkpoint");
        var expected = transaction.GetBytes();

        var buffer = new byte[expected.Length + 4]; // extra padding to catch over-writes
        transaction.WriteTo(buffer, 2);

        buffer[0].Should().Be(0); // padding before offset untouched
        buffer[1].Should().Be(0);
        for (var i = 0; i < expected.Length; i++)
        {
            buffer[2 + i].Should().Be(expected[i]);
        }

        buffer[2 + expected.Length].Should().Be(0); // padding after untouched
        buffer[3 + expected.Length].Should().Be(0);
    }

    [Fact]
    public void GetByteCount_MatchesGetBytesLength()
    {
        var transaction = new DataStreamsTransactionInfo("hello-world", 12345L, "my-cp");
        transaction.GetByteCount().Should().Be(transaction.GetBytes().Length);
    }
}
