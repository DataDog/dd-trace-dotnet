// <copyright file="PeerServiceHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Tagging;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Helper to set peer.service and peer.service.source tags
    /// </summary>
    internal static class PeerServiceHelpers
    {
        /// <summary>
        /// Sets peer.service tag for AwsSdk spans based on the service, environment, and region
        /// </summary>
        /// <param name="tags">AwsSdkTags for the current span</param>
        public static AwsSdkTags DerivePeerService(AwsSdkTags tags, bool isAwsLambda)
        {
            var service = tags.AwsService;
            var region = tags.Region;
            if (isAwsLambda && tags.Region != null)
            {
                switch (service)
                {
                    case "DynamoDB":
                        tags.PeerService = "dynamodb." + region + ".amazonaws.com";
                        break;
                    case "EventBridge":
                        tags.PeerService = "events." + region + ".amazonaws.com";
                        break;
                    case "Kinesis":
                        tags.PeerService = "kinesis." + region + ".amazonaws.com";
                        break;
                    case "S3":
                        var s3Tags = (AwsS3Tags)tags;
                        if (s3Tags.BucketName != null)
                        {
                            tags.PeerService = s3Tags.BucketName + ".s3." + region + ".amazonaws.com";
                        }
                        else
                        {
                            tags.PeerService = "s3." + region + ".amazonaws.com";
                        }

                        break;
                    case "SNS":
                        tags.PeerService = "sns." + region + ".amazonaws.com";
                        break;
                    case "SQS":
                        tags.PeerService = "sqs." + region + ".amazonaws.com";
                        break;
                }

                tags.PeerServiceSource = "peer.service";
            }
            else if (!isAwsLambda)
            {
                switch (service)
                {
                    case "DynamoDB":
                        if (tags is AwsDynamoDbTags)
                        {
                            var dbTags = (AwsDynamoDbTags)tags;
                            tags.PeerService = dbTags.TableName;
                            tags.PeerServiceSource = Trace.Tags.TableName;
                        }

                        break;
                    case "EventBridge":
                        if (tags is AwsEventBridgeTags)
                        {
                            var eventTags = (AwsEventBridgeTags)tags;
                            tags.PeerService = eventTags.RuleName;
                            tags.PeerServiceSource = Trace.Tags.RuleName;
                        }

                        break;
                    case "Kinesis":
                        if (tags is AwsKinesisTags)
                        {
                            var kinesisTags = (AwsKinesisTags)tags;
                            tags.PeerService = kinesisTags.StreamName;
                            tags.PeerServiceSource = Trace.Tags.StreamName;
                        }

                        break;
                    case "S3":
                        if (tags is AwsS3Tags)
                        {
                            var s3Tags = (AwsS3Tags)tags;
                            tags.PeerService = s3Tags.BucketName;
                            tags.PeerServiceSource = Trace.Tags.BucketName;
                        }

                        break;
                    case "SNS":
                        if (tags is AwsSnsTags)
                        {
                            var snsTags = (AwsSnsTags)tags;
                            tags.PeerService = snsTags.TopicName;
                            tags.PeerServiceSource = Trace.Tags.TopicName;
                        }

                        break;
                    case "SQS":
                        if (tags is AwsSqsTags)
                        {
                            var sqsTags = (AwsSqsTags)tags;
                            tags.PeerService = sqsTags.QueueName;
                            tags.PeerServiceSource = Trace.Tags.QueueName;
                        }

                        break;
                    case "StepFunctions":
                        if (tags is AwsStepFunctionsTags)
                        {
                            var stepTags = (AwsStepFunctionsTags)tags;
                            tags.PeerService = stepTags.StateMachineName;
                            tags.PeerServiceSource = Trace.Tags.StateMachineName;
                        }

                        break;
                }
            }

            return tags;
        }
    }
}
