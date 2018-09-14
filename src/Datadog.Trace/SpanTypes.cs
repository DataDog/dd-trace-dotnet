namespace Datadog.Trace
{
    /// <summary>
    /// Contains a set of standard span types that can be used by integrations.
    /// </summary>
    public static class SpanTypes
    {
        /// <summary>
        /// The span type for a redis database.
        /// </summary>
        public const string Redis = "redis";

        /// <summary>
        /// The span type for a sql database.
        /// </summary>
        public const string Sql = "sql";

        /// <summary>
        /// The span type for a web integration.
        /// </summary>
        public const string Web = "web";
    }
}
