// <copyright file="DataStreamsTransactionContainerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

[Collection(nameof(DataStreamsTransactionCacheTestCollection))]
public class DataStreamsTransactionContainerTest
{
    [Fact]
    public void ZerosAreTrimmedWhenSerialized()
    {
        DataStreamsTransactionInfo.ClearCache();
        var container = new DataStreamsTransactionContainer(1024);
        container.GetDataAndReset().Should().BeEmpty();

        var transaction = new DataStreamsTransactionInfo("1", 1, "1");
        container.Add(transaction);
        var bytes = container.GetDataAndReset();
        bytes.Should().BeEquivalentTo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 49 });
    }

    [Fact]
    public void Add_WhenRemainingSpaceExactlyEqualsTransactionSize_StoresDataCorrectly()
    {
        var transaction = new DataStreamsTransactionInfo("id", 1L, "cp");
        var expectedBytes = transaction.GetBytes();

        // buffer sized to exactly fit one transaction — no slack
        var container = new DataStreamsTransactionContainer(expectedBytes.Length);
        container.Add(transaction);
        container.GetDataAndReset().Should().BeEquivalentTo(expectedBytes);
    }

    [Fact]
    public void Add_MultipleTransactions_AccumulatesBytes()
    {
        var t1 = new DataStreamsTransactionInfo("aaa", 10L, "cp1");
        var t2 = new DataStreamsTransactionInfo("bbb", 20L, "cp2");
        var expected = t1.GetBytes().Concat(t2.GetBytes()).ToArray();

        var container = new DataStreamsTransactionContainer(1024);
        container.Add(t1);
        container.Add(t2);
        container.GetDataAndReset().Should().BeEquivalentTo(expected);
    }
}
