// <copyright file="RedisTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis
{
    internal partial class RedisTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.StackExchangeRedis);

        [Tag(Trace.Tags.RedisRawCommand)]
        public string RawCommand { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string Host { get; set; }

        [Tag(Trace.Tags.OutPort)]
        public string Port { get; set; }
    }
}
