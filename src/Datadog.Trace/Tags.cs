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
        public const string Env = "env";

        /// <summary>
        /// The version of the profiled service.
        /// </summary>
        public const string Version = "version";

        /// <summary>
        /// The name of the integration that generated the span.
        /// Use OpenTracing tag "component"
        /// </summary>
        public const string InstrumentationName = "component";

        /// <summary>
        /// The name of the method that was instrumented to generate the span.
        /// </summary>
        public const string InstrumentedMethod = "instrumented.method";

        /// <summary>
        /// The kind of span (e.g. client, server). Not to be confused with <see cref="Span.Type"/>.
        /// </summary>
        /// <seealso cref="SpanKinds"/>
        public const string SpanKind = "span.kind";

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
        /// The MVC or Web API area name.
        /// </summary>
        public const string AspNetArea = "aspnet.area";

        /// <summary>
        /// The ASP.NET routing template.
        /// </summary>
        public const string AspNetCoreRoute = "aspnet_core.route";

        /// <summary>
        /// The MVC or Web API controller name.
        /// </summary>
        public const string AspNetCoreController = "aspnet_core.controller";

        /// <summary>
        /// The MVC or Web API action name.
        /// </summary>
        public const string AspNetCoreAction = "aspnet_core.action";

        /// <summary>
        /// The MVC or Web API area name.
        /// </summary>
        public const string AspNetCoreArea = "aspnet_core.area";

        /// <summary>
        /// The Razor Pages page name.
        /// </summary>
        public const string AspNetCorePage = "aspnet_core.page";

        /// <summary>
        /// The Endpoint name in ASP.NET Core endpoint routing.
        /// </summary>
        public const string AspNetCoreEndpoint = "aspnet_core.endpoint";

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
        /// The AMQP method.
        /// </summary>
        public const string AmqpCommand = "amqp.command";

        /// <summary>
        /// The name of the AMQP exchange the message was originally published to.
        /// </summary>
        public const string AmqpExchange = "amqp.exchange";

        /// <summary>
        /// The routing key for the AMQP message.
        /// </summary>
        public const string AmqpRoutingKey = "amqp.routing_key";

        /// <summary>
        /// The name of the queue for the AMQP message.
        /// </summary>
        public const string AmqpQueue = "amqp.queue";

        /// <summary>
        /// The delivery mode of the AMQP message.
        /// </summary>
        public const string AmqpDeliveryMode = "amqp.delivery_mode";

        /// <summary>
        /// The size of the message.
        /// </summary>
        public const string MessageSize = "message.size";

        /// <summary>
        /// The sampling priority for the entire trace.
        /// </summary>
        public const string SamplingPriority = "sampling.priority";

        /// <summary>
        /// Obsolete. Use <see cref="ManualKeep"/>.
        /// </summary>
        [Obsolete("This field will be removed in futures versions of this library. Use ManualKeep instead.")]
        public const string ForceKeep = "force.keep";

        /// <summary>
        /// Obsolete. Use <see cref="ManualDrop"/>.
        /// </summary>
        [Obsolete("This field will be removed in futures versions of this library. Use ManualDrop instead.")]
        public const string ForceDrop = "force.drop";

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserKeep"/>.
        /// </summary>
        public const string ManualKeep = "manual.keep";

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserReject"/>.
        /// </summary>
        public const string ManualDrop = "manual.drop";

        /// <summary>
        /// Configures Trace Analytics.
        /// </summary>
        public const string Analytics = "_dd1.sr.eausr";

        /// <summary>
        /// Language tag, applied to root spans that are .NET runtime (e.g., ASP.NET)
        /// </summary>
        public const string Language = "language";

        /// <summary>
        /// The resource id of the site instance in Azure App Services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesResourceId = "aas.resource.id";

        /// <summary>
        /// The resource group of the site instance in Azure App Services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesResourceGroup = "aas.resource.group";

        /// <summary>
        /// The site name of the site instance in Azure where the traced application is running.
        /// </summary>
        public const string AzureAppServicesSiteName = "aas.site.name";

        /// <summary>
        /// The version of the extension installed where the traced application is running.
        /// </summary>
        public const string AzureAppServicesExtensionVersion = "aas.environment.extension_version";

        /// <summary>
        /// The instance name in Azure where the traced application is running.
        /// </summary>
        public const string AzureAppServicesInstanceName = "aas.environment.instance_name";

        /// <summary>
        /// The instance id in Azure where the traced application is running.
        /// </summary>
        public const string AzureAppServicesInstanceId = "aas.environment.instance_id";

        /// <summary>
        /// The operating system in Azure where the traced application is running.
        /// </summary>
        public const string AzureAppServicesOperatingSystem = "aas.environment.os";

        /// <summary>
        /// The runtime in Azure where the traced application is running.
        /// </summary>
        public const string AzureAppServicesRuntime = "aas.environment.runtime";

        /// <summary>
        /// The kind of application instance running in Azure.
        /// Possible values: app, api, mobileapp, app_linux, app_linux_container, functionapp, functionapp_linux, functionapp_linux_container
        /// </summary>
        public const string AzureAppServicesSiteKind = "aas.site.kind";

        /// <summary>
        /// The type of application instance running in Azure.
        /// Possible values: app, function
        /// </summary>
        public const string AzureAppServicesSiteType = "aas.site.type";

        /// <summary>
        /// The subscription id of the site instance in Azure App Services where the traced application is running.
        /// </summary>
        public const string AzureAppServicesSubscriptionId = "aas.subscription.id";

        /// <summary>
        /// Configures the origin of the trace
        /// </summary>
        public const string Origin = "_dd.origin";

        /// <summary>
        /// Configures the measured metric for a span.
        /// </summary>
        public const string Measured = "_dd.measured";

        internal const string ElasticsearchAction = "elasticsearch.action";

        internal const string ElasticsearchMethod = "elasticsearch.method";

        internal const string ElasticsearchUrl = "elasticsearch.url";
    }
}
