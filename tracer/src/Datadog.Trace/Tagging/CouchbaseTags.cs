// <copyright file="CouchbaseTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class CouchbaseTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.Couchbase);

        [Tag(Trace.Tags.CouchbaseSeedNodes)]
        public string SeedNodes { get; set; }

        [Tag(Trace.Tags.CouchbaseOperationCode)]
        public string OperationCode { get; set; }

        [Tag(Trace.Tags.CouchbaseOperationBucket)]
        public string Bucket { get; set; }

        [Tag(Trace.Tags.CouchbaseOperationKey)]
        public string Key { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string Host { get; set; }

        [Tag(Trace.Tags.OutPort)]
        public string Port { get; set; }
    }

    internal partial class CouchbaseV1Tags : CouchbaseTags
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
            get => _peerServiceOverride ?? SeedNodes ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : SeedNodes is not null
                            ? Trace.Tags.CouchbaseSeedNodes
                            : Trace.Tags.OutHost;
            }
        }
    }
}
