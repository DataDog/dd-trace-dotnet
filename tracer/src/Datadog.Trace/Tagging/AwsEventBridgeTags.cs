// <copyright file="AwsEventBridgeTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal sealed partial class AwsEventBridgeTags : AwsSdkTags
    {
        public AwsEventBridgeTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsEventBridgeTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        // TODO rename the `rulename` tag to `eventbusname` across all runtimes
        [Tag(Trace.Tags.RuleName)]
        public string? RuleName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }
}
