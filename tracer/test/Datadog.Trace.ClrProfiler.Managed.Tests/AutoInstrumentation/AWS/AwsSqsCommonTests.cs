// <copyright file="AwsSqsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS;

public class AwsSqsCommonTests
{
    [Fact]
    public void GetCorrectQueueName()
    {
        // It is guaranteed that the last element is going to be the `QueueName`
        const string queueUrl = "https://localhost:8080/00000000/my-queue-name";

        AwsSqsCommon.GetQueueName(queueUrl).Should().Be("my-queue-name");

        // When the request does not contain a `QueueUrl` it should return `null`
        AwsSqsCommon.GetQueueName(queueUrl).Should().Be(null);
    }
}
