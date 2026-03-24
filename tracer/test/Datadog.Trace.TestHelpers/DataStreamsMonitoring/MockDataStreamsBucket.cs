// <copyright file="MockDataStreamsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsBucket
{
    [Key(nameof(Start))]
    public ulong Start { get; set; }

    [Key(nameof(Duration))]
    public ulong Duration { get; set; }

    [Key(nameof(Stats))]
    public MockDataStreamsStatsPoint[] Stats { get; set; }

    [Key(nameof(Backlogs))]
    public MockDataStreamsBacklog[] Backlogs { get; set; }

    [Key(nameof(Transactions))]
    public byte[] Transactions { get; set; }

    [Key(nameof(TransactionCheckpointIds))]
    public byte[] TransactionCheckpointIds { get; set; }

    /// <summary>
    /// Decodes the raw binary transaction data in this bucket into structured records.
    /// Returns an empty list if the bucket contains no transaction data.
    /// </summary>
    public IReadOnlyList<MockDataStreamsTransaction> DecodeTransactions()
    {
        if (Transactions is null || Transactions.Length == 0)
        {
            return Array.Empty<MockDataStreamsTransaction>();
        }

        // Decode checkpoint ID → name mapping from TransactionCheckpointIds.
        // Binary format per entry: [checkpointId (1 byte)][nameLength (1 byte)][nameBytes (nameLength bytes)]
        var checkpointNames = new Dictionary<byte, string>();
        if (TransactionCheckpointIds is { Length: > 0 } cpIds)
        {
            int i = 0;
            while (i < cpIds.Length)
            {
                var id = cpIds[i++];
                var nameLen = cpIds[i++];
                var name = Encoding.UTF8.GetString(cpIds, i, nameLen);
                checkpointNames[id] = name;
                i += nameLen;
            }
        }

        // Decode transactions.
        // Binary format per entry: [checkpointId (1 byte)][timestampNs (8 bytes, big-endian)][idLength (1 byte)][idBytes (idLength bytes)]
        var result = new List<MockDataStreamsTransaction>();
        var data = Transactions;
        int pos = 0;
        while (pos < data.Length)
        {
            var checkpointId = data[pos++];

            var timestampNs =
                ((long)data[pos] << 56) | ((long)data[pos + 1] << 48) |
                ((long)data[pos + 2] << 40) | ((long)data[pos + 3] << 32) |
                ((long)data[pos + 4] << 24) | ((long)data[pos + 5] << 16) |
                ((long)data[pos + 6] << 8) | data[pos + 7];
            pos += 8;

            var idLen = data[pos++];
            var transactionId = Encoding.UTF8.GetString(data, pos, idLen);
            pos += idLen;

            checkpointNames.TryGetValue(checkpointId, out var checkpointName);
            result.Add(new MockDataStreamsTransaction(transactionId, checkpointName ?? checkpointId.ToString(), timestampNs));
        }

        return result;
    }
}
