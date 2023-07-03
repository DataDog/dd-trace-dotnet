// <copyright file="CosmosDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class CosmosDbTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.CosmosDb);

        [Tag(Trace.Tags.DbType)]
        public string DbType => "cosmosdb";

        [Tag(Trace.Tags.CosmosDbContainer)]
        public string ContainerId { get; set; }

        [Tag(Trace.Tags.DbName)]
        public string DatabaseId { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string Host { get; set; }

        public virtual void SetEndpoint(Uri endpoint)
        {
            Host = endpoint?.ToString();
        }
    }

    internal partial class CosmosDbV1Tags : CosmosDbTags
    {
        private string _peerServiceOverride = null;

        [Tag(Trace.Tags.OutPort)]
        public string Port { get; set; }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? DatabaseId ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : DatabaseId is not null
                            ? "db.name"
                            : "out.host";
            }
        }

        public override void SetEndpoint(Uri endpoint)
        {
            Host = endpoint?.Host;
            Port = endpoint?.Port.ToString();
        }
    }
}
