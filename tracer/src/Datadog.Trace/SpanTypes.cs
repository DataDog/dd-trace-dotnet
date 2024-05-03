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
        internal const string Redis = "redis";

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
        internal const string MongoDb = "mongodb";

        /// <summary>
        /// The span type for an outgoing HTTP integration.
        /// </summary>
        public const string Http = "http";

        /// <summary>
        /// The span type for a GraphQL integration.
        /// </summary>
        internal const string GraphQL = "graphql";

        /// <summary>
        /// The span type for a message queue integration.
        /// </summary>
        public const string Queue = "queue";

        /// <summary>
        /// The span type for a custom integration.
        /// </summary>
        public const string Custom = "custom";

        /// <summary>
        /// The span type for a Test integration.
        /// </summary>
        public const string Test = "test";

        /// <summary>
        /// The span type for a Test Suite integration.
        /// </summary>
        internal const string TestSuite = "test_suite_end";

        /// <summary>
        /// The span type for a Test Module integration.
        /// </summary>
        internal const string TestModule = "test_module_end";

        /// <summary>
        /// The span type for a Test Module integration.
        /// </summary>
        internal const string TestSession = "test_session_end";

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
        internal const string Aerospike = "aerospike";

        /// <summary>
        /// The span type for serverless integrations.
        /// </summary>
        public const string Serverless = "serverless";

        /// <summary>
        /// The span type for db integrations (including couchbase)
        /// </summary>
        public const string Db = "db";

        /// <summary>
        /// The span type for GRPC integrations
        /// </summary>
        internal const string Grpc = "grpc";

        /// <summary>
        /// The span type for System integrations
        /// </summary>
        internal const string System = "system";

        /// <summary>
        /// The span type for System integrations
        /// </summary>
        internal const string IastVulnerability = "vulnerability";

        /// <summary>
        /// The span type for DynamoDB integrations
        /// </summary>
        internal const string DynamoDb = "dynamodb";

        /// <summary>
        /// The span type for a Browser test integration.
        /// </summary>
        internal const string Browser = "browser";
    }
}
