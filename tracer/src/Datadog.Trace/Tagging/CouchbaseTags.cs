// <copyright file="CouchbaseTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class CouchbaseTags : InstrumentationTags
    {
        [TagName(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [TagName(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.Couchbase);

        [TagName(Trace.Tags.CouchbaseOperationCode)]
        public string OperationCode { get; set; }

        [TagName(Trace.Tags.CouchbaseOperationBucket)]
        public string Bucket { get; set; }

        [TagName(Trace.Tags.CouchbaseOperationKey)]
        public string Key { get; set; }

        [TagName(Trace.Tags.OutHost)]
        public string Host { get; set; }

        [TagName(Trace.Tags.OutPort)]
        public string Port { get; set; }
    }
}
