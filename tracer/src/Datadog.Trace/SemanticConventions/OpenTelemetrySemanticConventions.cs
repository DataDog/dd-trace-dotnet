// <copyright file="OpenTelemetrySemanticConventions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.SemanticConventions
{
    /// <summary>
    /// Standard Datadog span tags.
    /// </summary>
    public static partial class OpenTelemetrySemanticConventions
    {
        internal const string Service = "service.name";

        /// <summary>
        /// The environment of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="TraceContext.Environment"/>.
        /// </summary>
        public const string Env = "deployment.environment.name";

        /// <summary>
        /// The version of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="TraceContext.ServiceVersion"/>.
        /// </summary>
        public const string Version = "service.version";

        /// <summary>
        /// The error message of an exception
        /// </summary>
        public const string ErrorMsg = "error.message";

        /// <summary>
        /// The type of an exception
        /// </summary>
        public const string ErrorType = "error.type";

        /// <summary>
        /// The method of an HTTP request
        /// </summary>
        public const string HttpMethod = "http.request.method";

        /// <summary>
        /// The host of an HTTP request
        /// </summary>
        public const string HttpRequestHeadersHost = "http.request.headers.host";

        /// <summary>
        /// The status code of an HTTP response
        /// </summary>
        public const string HttpStatusCode = "http.response.status_code";

        /// <summary>
        /// The URL of an HTTP request
        /// </summary>
        public const string HttpUrl = "url.full";

        /// <summary>
        /// Only when span.kind: server. The user agent header received with the request.
        /// </summary>
        internal const string HttpUserAgent = "user_agent.original";

        /// <summary>
        /// The network destination name (host).
        /// </summary>
        internal const string NetworkDestinationName = "network.peer.name";

        /// <summary>
        /// The network destination port.
        /// </summary>
        internal const string NetworkDestinationPort = "network.peer.port";

        /// <summary>
        /// The kind of span (e.g. client, server). Not to be confused with <see cref="Span.Type"/>.
        /// </summary>
        /// <seealso cref="SpanKinds"/>
        public const string SpanKind = "span.kind";

        /// <summary>
        /// The hostname of a outgoing server connection.
        /// </summary>
        public const string OutHost = "server.address";

        /// <summary>
        /// The port of a outgoing server connection.
        /// </summary>
        public const string OutPort = "server.port";
    }
}
