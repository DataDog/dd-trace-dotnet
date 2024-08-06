// <copyright file="MongoDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal partial class MongoDbTags : InstrumentationTags
    {
        [Tag(Internal.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Internal.Tags.InstrumentationName)]
        public string InstrumentationName => MongoDbIntegration.IntegrationName;

        [Tag(Internal.Tags.DbName)]
        public string DbName { get; set; }

        [Tag(Internal.Tags.MongoDbQuery)]
        public string Query { get; set; }

        [Tag(Internal.Tags.MongoDbCollection)]
        public string Collection { get; set; }

        [Tag(Internal.Tags.OutHost)]
        public string Host { get; set; }

        [Tag(Internal.Tags.OutPort)]
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
        [Tag(Internal.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? DbName ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Internal.Tags.PeerServiceSource)]
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
