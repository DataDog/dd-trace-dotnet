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
        DataStreamsTransactionInfo.ClearCacheForTesting();
        var transaction = new DataStreamsTransactionInfo("1", 1, "1");
        transaction.TimestampNs.Should().Be(1);
        transaction.GetBytes().Should().BeEquivalentTo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 49 });
    }

    [Fact]
    public void TransactionInfoCacheSerializesCorrectly()
    {
        DataStreamsTransactionInfo.ClearCacheForTesting();
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

    [Fact]
    public void CheckpointId_DoesNotIncrement_WhenCheckpointAlreadyCached()
    {
        DataStreamsTransactionInfo.ClearCacheForTesting();

        // First use of "cp-a" gets id 1, first use of "cp-b" gets id 2.
        var a1 = new DataStreamsTransactionInfo("tx1", 1, "cp-a");
        var b1 = new DataStreamsTransactionInfo("tx2", 1, "cp-b");

        // Repeated uses of existing checkpoint names must not increment the counter.
        for (var i = 0; i < 10; i++)
        {
            _ = new DataStreamsTransactionInfo("tx3", 1, "cp-a");
            _ = new DataStreamsTransactionInfo("tx4", 1, "cp-b");
        }

        // A brand-new checkpoint must still get the next sequential id (3), not some higher value.
        var c1 = new DataStreamsTransactionInfo("tx5", 1, "cp-c");

        a1.GetBytes()[0].Should().Be(1);
        b1.GetBytes()[0].Should().Be(2);
        c1.GetBytes()[0].Should().Be(3, "counter must only advance for new checkpoint names");
    }

    [Theory]
    [InlineData(255)]  // exact limit — no truncation
    [InlineData(256)]  // one byte over
    [InlineData(512)]  // well over
    public void LongTransactionId_IsTruncatedTo255Bytes(int idByteLength)
    {
        var id = new string('a', idByteLength);
        var transaction = new DataStreamsTransactionInfo(id, 1L, "cp");
        var bytes = transaction.GetBytes();

        // byte at offset 9 is the encoded length; must never exceed 255
        bytes[9].Should().Be(255);
        // total payload length must match: 10 header bytes + encoded id length
        bytes.Length.Should().Be(10 + 255);
        // GetByteCount must agree
        transaction.GetByteCount().Should().Be(bytes.Length);
    }

    [Fact]
    public void LongTransactionId_FromByteArray_IsTruncatedTo255Bytes()
    {
        var idBytes = new byte[300];
        var transaction = new DataStreamsTransactionInfo(idBytes, 1L, "cp");
        var bytes = transaction.GetBytes();

        bytes[9].Should().Be(255);
        bytes.Length.Should().Be(10 + 255);
    }

    [Fact]
    public void LongTransactionId_WithMultiByteChars_IsTruncatedAtOrBelow255Bytes()
    {
        // 253 ASCII bytes + a 3-byte Chinese char: total 256 bytes, needs truncation.
        // With char-boundary truncation (Encoder.Convert), the Chinese char does not fit
        // in the remaining 2 bytes, so only 253 bytes are stored (not a partial sequence).
        var id = new string('a', 253) + "中";
        var transaction = new DataStreamsTransactionInfo(id, 1L, "cp");
        var bytes = transaction.GetBytes();

        var storedLength = bytes[9];
        storedLength.Should().BeLessOrEqualTo(255);
        bytes.Length.Should().Be(10 + storedLength);
        transaction.GetByteCount().Should().Be(bytes.Length);
    }

    [Fact]
    public void GetCacheBytes_LongCheckpointName_IsTruncatedTo255Bytes()
    {
        DataStreamsTransactionInfo.ClearCacheForTesting();
        // Checkpoint name of 300 ASCII bytes — exceeds MaxIdBytes (255)
        var longName = new string('x', 300);
        _ = new DataStreamsTransactionInfo("t1", 1, longName);

        var cache = DataStreamsTransactionInfo.GetCacheBytes();

        // Entry: [1 byte id] [1 byte name length (must be <= 255)] [N bytes name]
        cache.Length.Should().Be(2 + 255, "first pass must cap at 255 bytes, not 300");
        cache[0].Should().Be(1);
        cache[1].Should().Be(255, "length byte must not wrap");
    }

    [Fact]
    public void GetCacheBytes_MultipleEntries_SerializesAllEntries()
    {
        DataStreamsTransactionInfo.ClearCacheForTesting();
        _ = new DataStreamsTransactionInfo("t1", 1, "alpha");
        _ = new DataStreamsTransactionInfo("t2", 2, "beta");
        _ = new DataStreamsTransactionInfo("t3", 3, "gamma");

        var cache = DataStreamsTransactionInfo.GetCacheBytes();

        // Total: 3 entries × (1 id + 1 len) + 5 + 4 + 5 name bytes = 20 bytes
        cache.Length.Should().Be(20);

        // Parse the blob into a id→name map; ConcurrentDictionary.ToArray() has no guaranteed order,
        // so we verify contents without assuming a fixed serialization order.
        var byId = new Dictionary<byte, string>();
        var pos = 0;
        while (pos < cache.Length)
        {
            var id = cache[pos++];
            var len = cache[pos++];
            byId[id] = System.Text.Encoding.UTF8.GetString(cache, pos, len);
            pos += len;
        }

        byId.Should().HaveCount(3);
        byId[1].Should().Be("alpha");
        byId[2].Should().Be("beta");
        byId[3].Should().Be("gamma");
    }
}
