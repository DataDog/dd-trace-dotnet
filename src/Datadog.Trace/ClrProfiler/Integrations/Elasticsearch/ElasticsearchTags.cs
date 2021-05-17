// <copyright file="ElasticsearchTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class ElasticsearchTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] ElasticsearchTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<ElasticsearchTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<ElasticsearchTags, string>(Trace.Tags.ElasticsearchAction, t => t.Action, (t, v) => t.Action = v),
                new Property<ElasticsearchTags, string>(Trace.Tags.ElasticsearchMethod, t => t.Method, (t, v) => t.Method = v),
                new Property<ElasticsearchTags, string>(Trace.Tags.ElasticsearchUrl, t => t.Url, (t, v) => t.Url = v));

        public override string SpanKind => SpanKinds.Client;

        public string InstrumentationName => ElasticsearchNetCommon.ComponentValue;

        public string Action { get; set; }

        public string Method { get; set; }

        public string Url { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => ElasticsearchTagsProperties;
    }
}
