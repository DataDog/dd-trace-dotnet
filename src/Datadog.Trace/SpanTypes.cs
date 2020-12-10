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
        /// The span type for a message client integration.
        /// </summary>
        public const string MessageClient = "queue";

        /// <summary>
        /// The span type for a message consumer integration.
        /// </summary>
        public const string MessageConsumer = "queue";

        /// <summary>
        /// The span type for a message producer integration.
        /// </summary>
        public const string MessageProducer = "queue";

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
    }
}
