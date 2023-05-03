// <copyright file="AwsSnsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AwsSnsTags : AwsSdkTags
    {
        [Tag(Trace.Tags.AwsTopicName)]
        public string TopicName { get; set; }

        [Tag(Trace.Tags.TopicName)]
        public string TopLevelTopicName { get; set; }

        [Tag(Trace.Tags.AwsTopicArn)]
        public string TopicArn { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;
    }
}
