namespace Datadog.Trace
{
    /// <summary>
    /// This class contains a set of standard tags that can be used by integrations
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// The URL of an HTTP request
        /// </summary>
        public const string HttpUrl = "http.url";

        /// <summary>
        /// The method of an HTTP request
        /// </summary>
        public const string HttpMethod = "http.method";

        /// <summary>
        /// The status code of an HTTP response
        /// </summary>
        public const string HttpStatusCode = "http.status_code";

        /// <summary>
        /// The error message of an exception
        /// </summary>
        public const string ErrorMsg = "error.msg";

        /// <summary>
        /// The type of an exception
        /// </summary>
        public const string ErrorType = "error.type";

        /// <summary>
        /// The stack trace of an exception
        /// </summary>
        public const string ErrorStack = "error.stack";

        /// <summary>
        /// The query text
        /// </summary>
        public const string SqlQuery = "sql.query";

        /// <summary>
        /// The number of rows returned by a query
        /// </summary>
        public const string SqlRows = "sql.rows";

        /// <summary>
        /// The name of the database
        /// </summary>
        public const string SqlDatabase = "sql.db";
    }
}
