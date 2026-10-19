// <copyright file="AwsStepFunctionsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS;

public class AwsStepFunctionsCommonTests
{
    public static IEnumerable<object[]> SchemaSpanKindOperationNameData
        => new List<object[]>
        {
            new object[] { "v0", "client", "stepfunctions.request" },
            new object[] { "v0", "producer", "stepfunctions.request" },
            new object[] { "v1", "client", "aws.stepfunctions.request" },
            new object[] { "v1", "producer", "aws.stepfunctions.send" },
            new object[] { "v1", "consumer", "aws.stepfunctions.process" },
            new object[] { "v1", "server", "aws.stepfunctions.request" }
        };

    [Theory]
    [MemberData(nameof(SchemaSpanKindOperationNameData))]
    public async Task GetCorrectOperationName(string schemaVersion, string spanKind, string expected)
    {
        await using var tracer = GetTracer(schemaVersion);

        AwsStepFunctionsCommon.GetOperationName(tracer, spanKind).Should().Be(expected);
    }

    [Theory]
    // Unqualified state machine ARN.
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:MyStateMachine", "MyStateMachine")]
    // Version-qualified state machine ARN.
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:MyStateMachine:1", "MyStateMachine")]
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:MyStateMachine:10", "MyStateMachine")]
    // Alias-qualified state machine ARN.
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:MyStateMachine:PROD", "MyStateMachine")]
    // Distributed Map state label-qualified state machine ARN.
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:MyStateMachine/mapStateLabel", "MyStateMachine")]
    // A state machine literally named "stateMachine" must resolve to its name, not its qualifier.
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:stateMachine:2", "stateMachine")]
    // Non-commercial partitions use the same resource layout.
    [InlineData("arn:aws-us-gov:states:us-gov-west-1:123456789012:stateMachine:MyStateMachine:BLUE", "MyStateMachine")]
    [InlineData("arn:aws-cn:states:cn-north-1:123456789012:stateMachine:MyStateMachine:1", "MyStateMachine")]
    // Names are allowed to contain characters that are not ARN separators.
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:my-state_machine.v2:1", "my-state_machine.v2")]
    // The resource segment may start the input, and an absent name yields an empty name.
    [InlineData(":stateMachine:MyStateMachine", "MyStateMachine")]
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:", "")]
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine::1", "")]
    [InlineData("arn:aws:states:us-east-1:123456789012:stateMachine:/mapStateLabel", "")]
    public void GetStateMachineName_ReturnsStateMachineName(string stateMachineArn, string expected)
    {
        AwsStepFunctionsCommon.GetStateMachineName(stateMachineArn).Should().Be(expected);
    }

    [Fact]
    public void GetStateMachineName_ReturnsNull_ForNullArn()
    {
        AwsStepFunctionsCommon.GetStateMachineName(null).Should().BeNull();
    }

    [Theory]
    // Inputs without a `stateMachine` resource keep the previous trailing-segment fallback.
    [InlineData("", "")]
    [InlineData("MyStateMachine", "MyStateMachine")]
    [InlineData("not-an-arn:MyStateMachine", "MyStateMachine")]
    [InlineData("arn:aws:states:us-east-1:123456789012:execution:MyStateMachine:abc", "abc")]
    // The resource segment is matched case-sensitively, as it appears in an ARN.
    [InlineData("arn:aws:states:us-east-1:123456789012:statemachine:MyStateMachine:1", "1")]
    public void GetStateMachineName_FallsBackToTrailingSegment_ForUnrecognizedArn(string stateMachineArn, string expected)
    {
        AwsStepFunctionsCommon.GetStateMachineName(stateMachineArn).Should().Be(expected);
    }

    private static ScopedTracer GetTracer(string schemaVersion)
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }
}
