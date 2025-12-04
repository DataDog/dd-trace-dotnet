// <copyright file="DatadogHttpTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class DatadogHttpTags : HttpTags
    {
        private const string HttpClientHandlerTypeKey = "http-client-handler-type";

        [Tag(Trace.Tags.InstrumentationName)]
        public override string InstrumentationName { get; set; }

        [Tag(HttpClientHandlerTypeKey)]
        public string HttpClientHandlerType { get; set; }

        [Tag(Trace.Tags.HttpMethod)]
        public override string HttpMethod { get; set; }

        [Tag(Trace.Tags.HttpUrl)]
        public override string HttpUrl { get; set; }

        [Tag(Trace.Tags.HttpStatusCode)]
        public override string HttpStatusCode { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public override string Host { get; set; }
    }

    internal partial class DatadogHttpV1Tags : DatadogHttpTags
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
            get => _peerServiceOverride ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
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
