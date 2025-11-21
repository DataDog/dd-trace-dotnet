// <copyright file="AwsSqsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsSqsTags : AwsSdkTags
    {
        public AwsSqsTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsSqsTags(string spanKind)
        {
            SpanKind = spanKind;
        }

#pragma warning disable CS0618 // Duplicate of QueueName
        [Tag(Trace.Tags.AwsQueueName)]
#pragma warning restore CS0618
        public string? AwsQueueName => QueueName;

        [Tag(Trace.Tags.QueueName)]
        public string? QueueName { get; set; }

        [Tag(Trace.Tags.AwsQueueUrl)]
        public string? QueueUrl { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }
}
