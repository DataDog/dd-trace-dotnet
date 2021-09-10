// <copyright file="RedisTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis
{
    internal class RedisTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] RedisTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<RedisTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<RedisTags, string>(Trace.Tags.RedisRawCommand, t => t.RawCommand, (t, v) => t.RawCommand = v),
                new Property<RedisTags, string>(Trace.Tags.OutPort, t => t.Port, (t, v) => t.Port = v),
                new Property<RedisTags, string>(Trace.Tags.OutHost, t => t.Host, (t, v) => t.Host = v));

        public override string SpanKind => SpanKinds.Client;

        public string InstrumentationName => RedisBatch.IntegrationName;

        public string RawCommand { get; set; }

        public string Host { get; set; }

        public string Port { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => RedisTagsProperties;
    }
}
