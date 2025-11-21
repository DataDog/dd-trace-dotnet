// <copyright file="AwsSnsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsSnsTags : AwsSdkTags
    {
        public AwsSnsTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsSnsTags(string spanKind)
        {
            SpanKind = spanKind;
        }

#pragma warning disable CS0618 // Duplicate of TopicName
        [Tag(Trace.Tags.AwsTopicName)]
#pragma warning restore CS0618
        public string? AwsTopicName => TopicName;

        [Tag(Trace.Tags.TopicName)]
        public string? TopicName { get; set; }

        [Tag(Trace.Tags.AwsTopicArn)]
        public string? TopicArn { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }
}
