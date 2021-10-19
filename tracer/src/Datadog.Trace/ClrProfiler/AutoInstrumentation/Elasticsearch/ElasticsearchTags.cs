// <copyright file="ElasticsearchTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch
{
    internal partial class ElasticsearchTags : InstrumentationTags
    {
        [TagName(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [TagName(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => ElasticsearchNetCommon.ComponentValue;

        [TagName(Trace.Tags.ElasticsearchAction)]
        public string Action { get; set; }

        [TagName(Trace.Tags.ElasticsearchMethod)]
        public string Method { get; set; }

        [TagName(Trace.Tags.ElasticsearchUrl)]
        public string Url { get; set; }
    }
}
