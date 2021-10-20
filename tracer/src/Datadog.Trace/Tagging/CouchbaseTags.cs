// <copyright file="CouchbaseTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class CouchbaseTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] CouchbaseTagsProperties =
            InstrumentationTagsProperties.Concat(
                new ReadOnlyProperty<CouchbaseTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName),
                new Property<CouchbaseTags, string>(Trace.Tags.CouchbaseOperationCode, t => t.OperationCode, (t, v) => t.OperationCode = v),
                new Property<CouchbaseTags, string>(Trace.Tags.CouchbaseOperationKey, t => t.Key, (t, v) => t.Key = v),
                new Property<CouchbaseTags, string>(Trace.Tags.CouchbaseOperationHost, t => t.Host, (t, v) => t.Host = v),
                new Property<CouchbaseTags, string>(Trace.Tags.CouchbaseOperationPort, t => t.Port, (t, v) => t.Port = v));

        public override string SpanKind => SpanKinds.Client;

        public string InstrumentationName => nameof(IntegrationIds.Couchbase);

        public string OperationCode { get; set; }

        public string Key { get; set; }

        public string Host { get; set; }

        public string Port { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => CouchbaseTagsProperties;
    }
}
