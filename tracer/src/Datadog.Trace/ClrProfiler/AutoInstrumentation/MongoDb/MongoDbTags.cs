// <copyright file="MongoDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal partial class MongoDbTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => MongoDbIntegration.IntegrationName;

        [Tag(Trace.Tags.DbName)]
        public string DbName { get; set; }

        [Tag(Trace.Tags.MongoDbQuery)]
        public string Query { get; set; }

        [Tag(Trace.Tags.MongoDbCollection)]
        public string Collection { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string Host { get; set; }

        [Tag(Trace.Tags.OutPort)]
        public string Port { get; set; }
    }
}
