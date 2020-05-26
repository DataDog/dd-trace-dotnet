using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Standard span tags used by integrations.
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// The environment of the profiled service.
        /// </summary>
        public const string Env = CoreTags.Env;

        /// <summary>
        /// The version of the profiled service.
        /// </summary>
        public const string Version = "version";

        /// <summary>
        /// The name of the integration that generated the span.
        /// Use OpenTracing tag "component"
        /// </summary>
        public const string InstrumentationName = CoreTags.InstrumentationName;

        /// <summary>
        /// The name of the method that was instrumented to generate the span.
        /// </summary>
        public const string InstrumentedMethod = CoreTags.InstrumentedMethod;

        /// <summary>
        /// The kind of span (e.g. client, server). Not to be confused with <see cref="Span.Type"/>.
        /// </summary>
        /// <seealso cref="SpanKinds"/>
        public const string SpanKind = CoreTags.SpanKind;

        /// <summary>
        /// The URL of an HTTP request
        /// </summary>
        public const string HttpUrl = "http.url";

        /// <summary>
        /// The method of an HTTP request
        /// </summary>
        public const string HttpMethod = "http.method";

        /// <summary>
        /// The host of an HTTP request
        /// </summary>
        public const string HttpRequestHeadersHost = "http.request.headers.host";

        /// <summary>
        /// The status code of an HTTP response
        /// </summary>
        public const string HttpStatusCode = "http.status_code";

        /// <summary>
        /// The error message of an exception
        /// </summary>
        public const string ErrorMsg = CoreTags.ErrorMsg;

        /// <summary>
        /// The type of an exception
        /// </summary>
        public const string ErrorType = CoreTags.ErrorType;

        /// <summary>
        /// The stack trace of an exception
        /// </summary>
        public const string ErrorStack = CoreTags.ErrorStack;

        /// <summary>
        /// The type of database (e.g. mssql, mysql)
        /// </summary>
        public const string DbType = "db.type";

        /// <summary>
        /// The user used to sign into a database
        /// </summary>
        public const string DbUser = "db.user";

        /// <summary>
        /// The name of the database.
        /// </summary>
        public const string DbName = "db.name";

        /// <summary>
        /// The query text
        /// </summary>
        public const string SqlQuery = "sql.query";

        /// <summary>
        /// The number of rows returned by a query
        /// </summary>
        public const string SqlRows = "sql.rows";

        /// <summary>
        /// The ASP.NET routing template.
        /// </summary>
        public const string AspNetRoute = "aspnet.route";

        /// <summary>
        /// The MVC or Web API controller name.
        /// </summary>
        public const string AspNetController = "aspnet.controller";

        /// <summary>
        /// The MVC or Web API action name.
        /// </summary>
        public const string AspNetAction = "aspnet.action";

        /// <summary>
        /// The hostname of a outgoing server connection.
        /// </summary>
        public const string OutHost = "out.host";

        /// <summary>
        /// The port of a outgoing server connection.
        /// </summary>
        public const string OutPort = "out.port";

        /// <summary>
        /// The raw command sent to Redis.
        /// </summary>
        public const string RedisRawCommand = "redis.raw_command";

        /// <summary>
        /// A MongoDB query.
        /// </summary>
        public const string MongoDbQuery = "mongodb.query";

        /// <summary>
        /// A MongoDB collection name.
        /// </summary>
        public const string MongoDbCollection = "mongodb.collection";

        /// <summary>
        /// The operation name of the GraphQL request.
        /// </summary>
        public const string GraphQLOperationName = "graphql.operation.name";

        /// <summary>
        /// The operation type of the GraphQL request.
        /// </summary>
        public const string GraphQLOperationType = "graphql.operation.type";

        /// <summary>
        /// The source defining the GraphQL request.
        /// </summary>
        public const string GraphQLSource = "graphql.source";

        /// <summary>
        /// The sampling priority for the entire trace.
        /// </summary>
        public const string SamplingPriority = CoreTags.SamplingPriority;

        /// <summary>
        /// Obsolete. Use <see cref="ManualKeep"/>.
        /// </summary>
        [Obsolete("This field will be removed in futures versions of this library. Use ManualKeep instead.")]
        public const string ForceKeep = CoreTags.ForceKeep;

        /// <summary>
        /// Obsolete. Use <see cref="ManualDrop"/>.
        /// </summary>
        [Obsolete("This field will be removed in futures versions of this library. Use ManualDrop instead.")]
        public const string ForceDrop = CoreTags.ForceDrop;

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserKeep"/>.
        /// </summary>
        public const string ManualKeep = CoreTags.ManualKeep;

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserReject"/>.
        /// </summary>
        public const string ManualDrop = CoreTags.ManualDrop;

        /// <summary>
        /// Configures Trace Analytics.
        /// </summary>
        public const string Analytics = CoreTags.Analytics;

        /// <summary>
        /// Language tag, applied to root spans that are .NET runtime (e.g., ASP.NET)
        /// </summary>
        public const string Language = CoreTags.Language;

        /// <summary>
        /// The resource id of the site instance in azure app services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesResourceId = "aas.resource.id";

        /// <summary>
        /// The resource group of the site instance in azure app services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesResourceGroup = "aas.resource.group";

        /// <summary>
        /// The site name of the site instance in azure app services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesSiteName = "aas.site.name";

        /// <summary>
        /// The subscription id of the site instance in azure app services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesSubscriptionId = "aas.subscription.id";
    }
}
