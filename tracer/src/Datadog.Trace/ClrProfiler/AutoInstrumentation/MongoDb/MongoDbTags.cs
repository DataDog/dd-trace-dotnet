// <copyright file="MongoDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

#pragma warning disable SA1402 // File must contain single type
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

    internal partial class MongoDbV1Tags : MongoDbTags
    {
        private string _peerServiceOverride = null;

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? DbName ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : DbName is not null
                            ? "db.name"
                            : "out.host";
            }
        }
    }
}
