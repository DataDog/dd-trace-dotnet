// <copyright file="SqlTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class SqlTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.DbType)]
        public string DbType { get; set; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName { get; set; }

        [Tag(Trace.Tags.DbName)]
        public string DbName { get; set; }

        [Tag(Trace.Tags.DbUser)]
        public string DbUser { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string OutHost { get; set; }
    }
}
