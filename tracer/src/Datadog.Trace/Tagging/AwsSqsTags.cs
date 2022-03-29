// <copyright file="AwsSqsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AwsSqsTags : AwsSdkTags
    {
        [Tag(Trace.Tags.AwsQueueName)]
        public string QueueName { get; set; }

        [Tag(Trace.Tags.AwsQueueUrl)]
        public string QueueUrl { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;
    }
}
