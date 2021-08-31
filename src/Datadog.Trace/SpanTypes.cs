// <copyright file="SpanTypes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// A set of standard span types that can be used by integrations.
    /// Not to be confused with span kinds.
    /// </summary>
    /// <seealso cref="SpanKinds"/>
    public static class SpanTypes
    {
        /// <summary>
        /// The span type for a Redis client integration.
        /// </summary>
        public const string Redis = "redis";

        /// <summary>
        /// The span type for a SQL client integration.
        /// </summary>
        public const string Sql = "sql";

        /// <summary>
        /// The span type for a web framework integration (incoming HTTP requests).
        /// </summary>
        public const string Web = "web";

        /// <summary>
        /// The span type for a MongoDB integration.
        /// </summary>
        public const string MongoDb = "mongodb";

        /// <summary>
        /// The span type for an outgoing HTTP integration.
        /// </summary>
        public const string Http = "http";

        /// <summary>
        /// The span type for a GraphQL integration.
        /// </summary>
        public const string GraphQL = "graphql";

        /// <summary>
        /// The span type for a message queue integration.
        /// </summary>
        public const string Queue = "queue";

        /// <summary>
        /// The span type for a custom integration.
        /// </summary>
        public const string Custom = "custom";

        /// <summary>
        /// The span type for a Test instegration.
        /// </summary>
        public const string Test = "test";

        /// <summary>
        /// The span type for a Benchmark integration.
        /// </summary>
        public const string Benchmark = "benchmark";

        /// <summary>
        /// The span type for msbuild integration.
        /// </summary>
        public const string Build = "build";

        /// <summary>
        /// The span type for an Aerospike integration.
        /// </summary>
        public const string Aerospike = "aerospike";
    }
}
