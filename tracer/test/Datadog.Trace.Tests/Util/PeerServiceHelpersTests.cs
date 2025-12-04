// <copyright file="PeerServiceHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Util;

extern alias DatadogTraceManual;

public class PeerServiceHelpersTests
{
    private readonly ITestOutputHelper output;

    public PeerServiceHelpersTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData("DynamoDB", true, false)]
    [InlineData("DynamoDB", false, false)]
    [InlineData("EventBridge", true, false)]
    [InlineData("EventBridge", false, false)]
    [InlineData("Kinesis", true, false)]
    [InlineData("Kinesis", false, false)]
    [InlineData("S3", true, false)]
    [InlineData("S3", false, false)]
    [InlineData("S3", true, true)]
    [InlineData("S3", false, true)]
    [InlineData("SNS", true, false)]
    [InlineData("SNS", false, false)]
    [InlineData("SQS", true, false)]
    [InlineData("SQS", false, false)]
    [InlineData("StepFunctions", false, false)]
    public void DerivePeerService(string service, bool isAwsLambda, bool hasBucket)
    {
        switch (service)
        {
            case "DynamoDB":
            {
                var tags = new AwsDynamoDbTags();
                tags.Service = "DynamoDB";
                tags.Region = "us-east-1";
                tags.TableName = "example-table";
                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                if (isAwsLambda)
                {
                    tags.PeerService.Should().Be("dynamodb.us-east-1.amazonaws.com");
                    tags.PeerServiceSource.Should().Be("peer.service");
                }
                else
                {
                    tags.PeerService.Should().Be("example-table");
                    tags.PeerServiceSource.Should().Be("tablename");
                }

                break;
            }

            case "EventBridge":
            {
                var tags = new AwsEventBridgeTags();
                tags.Service = "EventBridge";
                tags.Region = "us-east-1";
                tags.RuleName = "example-rule";
                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                if (isAwsLambda)
                {
                    tags.PeerService.Should().Be("events.us-east-1.amazonaws.com");
                    tags.PeerServiceSource.Should().Be("peer.service");
                }
                else
                {
                    tags.PeerService.Should().Be("example-rule");
                    tags.PeerServiceSource.Should().Be("rulename");
                }

                break;
            }

            case "Kinesis":
            {
                var tags = new AwsKinesisTags(SpanKinds.Client);
                tags.Service = "Kinesis";
                tags.Region = "us-east-1";
                tags.StreamName = "example-stream";
                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                if (isAwsLambda)
                {
                    tags.PeerService.Should().Be("kinesis.us-east-1.amazonaws.com");
                    tags.PeerServiceSource.Should().Be("peer.service");
                }
                else
                {
                    tags.PeerService.Should().Be("example-stream");
                    tags.PeerServiceSource.Should().Be("streamname");
                }

                break;
            }

            case "S3":
            {
                var tags = new AwsS3Tags();
                tags.Service = "S3";
                tags.Region = "us-east-1";
                if (hasBucket)
                {
                    tags.BucketName = "example-bucket";
                }

                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                if (isAwsLambda)
                {
                    if (hasBucket)
                    {
                        tags.PeerService.Should().Be("example-bucket.s3.us-east-1.amazonaws.com");
                    }
                    else
                    {
                        tags.PeerService.Should().Be("s3.us-east-1.amazonaws.com");
                    }

                    tags.PeerServiceSource.Should().Be("peer.service");
                }
                else
                {
                    tags.PeerService.Should().Be(tags.BucketName);
                    tags.PeerServiceSource.Should().Be("bucketname");
                }

                break;
            }

            case "SNS":
            {
                var tags = new AwsSnsTags();
                tags.Service = "SNS";
                tags.Region = "us-east-1";
                tags.TopicName = "example-topic";
                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                if (isAwsLambda)
                {
                    tags.PeerService.Should().Be("sns.us-east-1.amazonaws.com");
                    tags.PeerServiceSource.Should().Be("peer.service");
                }
                else
                {
                    tags.PeerService.Should().Be("example-topic");
                    tags.PeerServiceSource.Should().Be("topicname");
                }

                break;
            }

            case "SQS":
            {
                var tags = new AwsSqsTags();
                tags.Service = "SQS";
                tags.Region = "us-east-1";
                tags.QueueName = "example-queue";
                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                if (isAwsLambda)
                {
                    tags.PeerService.Should().Be("sqs.us-east-1.amazonaws.com");
                    tags.PeerServiceSource.Should().Be("peer.service");
                }
                else
                {
                    tags.PeerService.Should().Be("example-queue");
                    tags.PeerServiceSource.Should().Be("queuename");
                }

                break;
            }

            case "StepFunctions":
            {
                var tags = new AwsStepFunctionsTags(SpanKinds.Client);
                tags.Service = "StepFunctions";
                tags.Region = "us-east-1";
                tags.StateMachineName = "example-state-machine";
                PeerServiceHelpers.DerivePeerService(tags, isAwsLambda);
                tags.PeerService.Should().Be("example-state-machine");
                tags.PeerServiceSource.Should().Be("statemachinename");
                break;
            }
        }
    }
}
