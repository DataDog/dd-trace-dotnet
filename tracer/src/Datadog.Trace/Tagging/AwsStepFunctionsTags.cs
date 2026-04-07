// <copyright file="AwsStepFunctionsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal sealed partial class AwsStepFunctionsTags : AwsSdkTags
    {
        [Obsolete("Use constructor that takes a SpanKind")]
        public AwsStepFunctionsTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsStepFunctionsTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.StateMachineName)]
        public string? StateMachineName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }
}
