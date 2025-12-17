// <copyright file="DataStreamsTransactionContainerTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsTransactionContainerTest
{
    [Fact]
    public void ZerosAreTrimmedWhenSerialized()
    {
        var container = new DataStreamsTransactionContainer(1024);
        container.GetDataAndReset().Should().BeEmpty();

        var transaction = new DataStreamsTransactionInfo("1", 1, "1");
        container.Add(transaction);
        var bytes = container.GetDataAndReset();
        bytes.Should().BeEquivalentTo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 49 });
    }
}
