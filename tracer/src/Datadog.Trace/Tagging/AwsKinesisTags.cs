// <copyright file="AwsKinesisTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tagging
{
    internal partial class AwsKinesisTags : AwsSdkTags
    {
        public AwsKinesisTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.StreamName)]
        public string StreamName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }
}
