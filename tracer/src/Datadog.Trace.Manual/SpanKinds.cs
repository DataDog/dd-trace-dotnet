// <copyright file="SpanKinds.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// A set of standard span kinds that can be used by integrations.
    /// Not to be confused with span types.
    /// </summary>
    /// <seealso cref="SpanTypes"/>
    public static class SpanKinds
    {
        /// <summary>
        /// A span generated by the client in a client/server architecture.
        /// </summary>
        /// <seealso cref="Tags.SpanKind"/>
        public const string Client = "client";

        /// <summary>
        /// A span generated by the server in a client/server architecture.
        /// </summary>
        /// <seealso cref="Tags.SpanKind"/>
        public const string Server = "server";

        /// <summary>
        /// A span generated by the producer in a producer/consumer architecture.
        /// </summary>
        /// <seealso cref="Tags.SpanKind"/>
        public const string Producer = "producer";

        /// <summary>
        /// A span generated by the consumer in a producer/consumer architecture.
        /// </summary>
        /// <seealso cref="Tags.SpanKind"/>
        public const string Consumer = "consumer";

        /// <summary>
        /// A span that represents an internal operation within an application.
        /// </summary>
        /// <seealso cref="Tags.SpanKind"/>
        public const string Internal = "internal";
    }
}