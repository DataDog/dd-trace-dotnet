// <copyright file="DataStreamsTransactionInfoTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsTransactionInfoTest
{
    [Fact]
    public void TransactionInfoSerializesCorrectly()
    {
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
}
