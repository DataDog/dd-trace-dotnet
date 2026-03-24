// <copyright file="MockDataStreamsTransaction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

/// <summary>
/// A decoded DSM transaction record extracted from a <see cref="MockDataStreamsBucket"/>.
/// </summary>
public class MockDataStreamsTransaction
{
    public MockDataStreamsTransaction(string transactionId, string checkpointName, long timestampNs)
    {
        TransactionId = transactionId;
        CheckpointName = checkpointName;
        TimestampNs = timestampNs;
    }

    public string TransactionId { get; }

    public string CheckpointName { get; }

    public long TimestampNs { get; }
}
