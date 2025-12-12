// <copyright file="AwsS3Tags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal sealed partial class AwsS3Tags : AwsSdkTags
    {
        public AwsS3Tags()
            : this(SpanKinds.Client)
        {
        }

        public AwsS3Tags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.BucketName)]
        public string? BucketName { get; set; }

        [Tag(Trace.Tags.ObjectKey)]
        public string? ObjectKey { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }
}
