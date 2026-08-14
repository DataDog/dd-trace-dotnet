// <copyright file="HttpTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class HttpTags : InstrumentationTags, IHasStatusCode
    {
        private const string HttpClientHandlerTypeKey = "http-client-handler-type";

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName { get; set; }

        [Tag(Trace.Tags.HttpMethod, OtelName = Trace.Tags.HttpRequestMethod)]
        public string HttpMethod { get; set; }

        /// <summary>
        /// Gets or sets the original HTTP method, when it differs from the value reported
        /// in <see cref="HttpMethod"/>. This is an OpenTelemetry-only concept, so it is only
        /// set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Trace.Tags.HttpRequestMethodOriginal)]
        public string HttpRequestMethodOriginal { get; set; }

        /// <summary>
        /// Gets or sets the request URL. Serialized as "http.url" with Datadog semantics
        /// and as "url.full" with OpenTelemetry semantics.
        /// </summary>
        [Tag(Trace.Tags.HttpUrl, OtelName = Trace.Tags.UrlFull)]
        public string HttpUrl { get; set; }

        [Tag(HttpClientHandlerTypeKey)]
        public string HttpClientHandlerType { get; set; }

        [Tag(Trace.Tags.HttpStatusCode, OtelName = Trace.Tags.HttpResponseStatusCode)]
        public int? HttpStatusCode { get; set; }

        [Tag(Trace.Tags.OutHost, OtelName = Trace.Tags.ServerAddress)]
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the port of the remote server. We have never reported a port for
        /// HTTP client spans with Datadog semantics, so this is only set when OpenTelemetry
        /// semantics are enabled.
        /// </summary>
        [Tag(Trace.Tags.ServerPort)]
        public int? ServerPort { get; set; }

        /// <summary>
        /// Gets or sets the version of the protocol negotiated with the remote server, as
        /// reported by the response. This is an OpenTelemetry-only concept, so it is only
        /// set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Trace.Tags.NetworkProtocolVersion)]
        public string NetworkProtocolVersion { get; set; }
    }

    internal sealed partial class HttpV1Tags : HttpTags
    {
        private string _peerServiceOverride;

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
                // Do not update this when OpenTelemetry semantics are enabled
                // since OpenTelemetry semantics supercedes V1Tags
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : "out.host";
            }
        }
    }
}
