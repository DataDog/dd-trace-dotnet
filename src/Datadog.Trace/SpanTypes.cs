namespace Datadog.Trace
{
    /// <summary>
    /// Contains a set of standard span types that can be used by integrations.
    /// </summary>
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
    }
}
