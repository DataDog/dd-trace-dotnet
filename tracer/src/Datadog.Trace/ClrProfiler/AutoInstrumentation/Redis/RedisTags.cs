// <copyright file="RedisTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis
{
    internal partial class RedisTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string? InstrumentationName { get; set; }

        [Tag(Trace.Tags.RedisRawCommand)]
        public string? RawCommand { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string? Host { get; set; }

        [Tag(Trace.Tags.OutPort)]
        public string? Port { get; set; }

        // Always use metrics for "number like" tags. Even though it's not really a "metric"
        // that should be summed/averaged, it's important to record it as such so that we
        // don't use "string-like" searching/sorting. e.g. sorting db.redis.database_index
        // should give 1, 2, 3... not 1, 10, 2... as it would otherwise.
        [Metric(Trace.Metrics.RedisDatabaseIndex)]
        public double? DatabaseIndex { get; set; }
    }

    internal partial class RedisV1Tags : RedisTags
    {
        private string? _peerServiceOverride = null;

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string? PeerService
        {
            get => _peerServiceOverride ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                           ? "peer.service"
                           : "out.host";
            }
        }
    }
}
