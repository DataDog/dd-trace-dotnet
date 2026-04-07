// <copyright file="HangfireTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
#nullable enable
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal sealed partial class HangfireTags : InstrumentationTags
    {
        public HangfireTags()
        {
        }

        [Tag(Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.Hangfire);

        [Tag(Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Internal;

        [Tag(Tags.HangfireJobCreatedAt)]
        public string? CreatedAt { get; set; }

        [Tag(Tags.HangfireJobId)]
        public string? JobId { get; set; }
    }
}
