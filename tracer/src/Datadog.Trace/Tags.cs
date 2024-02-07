// <copyright file="Tags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Standard Datadog span tags.
    /// </summary>
    public static partial class Tags
    {
        /// <summary>
        /// The name of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="SpanContext.ServiceName"/>
        /// or <see cref="Tracer.DefaultServiceName"/>.
        /// </summary>
        internal const string Service = "service";

        /// <summary>
        /// The environment of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="TraceContext.Environment"/>.
        /// </summary>
        public const string Env = "env";

        /// <summary>
        /// The version of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="TraceContext.ServiceVersion"/>.
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
        /// The type of database (e.g. "sql-server", "mysql", "postgres", "sqlite", "oracle")
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
        /// The service name of a remote service.
        /// </summary>
        public const string PeerService = "peer.service";

        /// <summary>
        /// The orignal peer service name before remapping
        /// </summary>
        internal const string PeerServiceRemappedFrom = "peer.service.remapped_from";

        /// <summary>
        /// The name of the attribute that determined the peer.service tag value. Expected values are:
        /// <ul>
        ///   <li>{source_attribute} when the tag was set to a default value, using a defined precursor attribute</li>
        ///   <li>peer.service when the tag was set by the user</li>
        /// </ul>
        /// </summary>
        internal const string PeerServiceSource = "_dd.peer.service.source";

        /// <summary>
        /// The hostname of a outgoing server connection.
        /// </summary>
        public const string OutHost = "out.host";

        /// <summary>
        /// Remote hostname.
        /// </summary>
        public const string PeerHostname = "peer.hostname";

        /// <summary>
        /// The port of a outgoing server connection.
        /// </summary>
        public const string OutPort = "out.port";

        /// <summary>
        /// The size of the message.
        /// </summary>
        public const string MessageSize = "message.size";

        /// <summary>
        /// The sampling priority for the entire trace.
        /// </summary>
        public const string SamplingPriority = "sampling.priority";

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserKeep"/>.
        /// </summary>
        public const string ManualKeep = "manual.keep";

        /// <summary>
        /// A user-friendly tag that sets the sampling priority to <see cref="Trace.SamplingPriority.UserReject"/>.
        /// </summary>
        public const string ManualDrop = "manual.drop";

        /// <summary>
        /// Language tag, applied to all spans that are .NET runtime (e.g. ASP.NET).
        /// This tag is added during MessagePack serialization. It's value is always "dotnet".
        /// </summary>
        public const string Language = "language";

        /// <summary>
        /// Pseudo-tag used to expose the complete trace id as a hex string.
        /// The string will have length 16 for 64-bit trace ids and length 32 for 128-bit trace ids.
        /// </summary>
        internal const string TraceId = "trace.id";

        /// <summary>
        /// The git commit hash of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="Datadog.Trace.Agent.MessagePack.TraceChunkModel.GitCommitSha"/>.
        /// </summary>
        internal const string GitCommitSha = "_dd.git.commit.sha";

        /// <summary>
        /// The git repository URL of the instrumented service. Its value is usually constant for the lifetime of a process,
        /// but can technically change for each trace if the user sets it manually.
        /// This tag is added during MessagePack serialization using the value from <see cref="Datadog.Trace.Agent.MessagePack.TraceChunkModel.GitRepositoryUrl"/>.
        /// </summary>
        internal const string GitRepositoryUrl = "_dd.git.repository_url";

        /// <summary>
        /// The end point requested
        /// </summary>
        internal const string HttpEndpoint = "http.endpoint";

        /// <summary>
        /// Only when span.kind: server. The matched route(path template).
        /// </summary>
        internal const string HttpRoute = "http.route";

        /// <summary>
        /// Only when span.kind: server. The user agent header received with the request.
        /// </summary>
        internal const string HttpUserAgent = "http.useragent";

        /// <summary>
        /// The IP address of the original client behind all proxies, if known (e.g. from X-Forwarded-For).
        /// </summary>
        internal const string HttpClientIp = "http.client_ip";

        /// <summary>
        /// The ASP.NET routing template.
        /// </summary>
        internal const string AspNetRoute = "aspnet.route";

        /// <summary>
        /// The MVC or Web API controller name.
        /// </summary>
        internal const string AspNetController = "aspnet.controller";

        /// <summary>
        /// The MVC or Web API action name.
        /// </summary>
        internal const string AspNetAction = "aspnet.action";

        /// <summary>
        /// The MVC or Web API area name.
        /// </summary>
        internal const string AspNetArea = "aspnet.area";

        /// <summary>
        /// The ASP.NET routing template.
        /// </summary>
        internal const string AspNetCoreRoute = "aspnet_core.route";

        /// <summary>
        /// The MVC or Web API controller name.
        /// </summary>
        internal const string AspNetCoreController = "aspnet_core.controller";

        /// <summary>
        /// The MVC or Web API action name.
        /// </summary>
        internal const string AspNetCoreAction = "aspnet_core.action";

        /// <summary>
        /// The MVC or Web API area name.
        /// </summary>
        internal const string AspNetCoreArea = "aspnet_core.area";

        /// <summary>
        /// The Razor Pages page name.
        /// </summary>
        internal const string AspNetCorePage = "aspnet_core.page";

        /// <summary>
        /// The Endpoint name in ASP.NET Core endpoint routing.
        /// </summary>
        internal const string AspNetCoreEndpoint = "aspnet_core.endpoint";

        /// <summary>
        /// The raw command sent to Redis.
        /// </summary>
        internal const string RedisRawCommand = "redis.raw_command";

        /// <summary>
        /// A MongoDB query.
        /// </summary>
        internal const string MongoDbQuery = "mongodb.query";

        /// <summary>
        /// A MongoDB collection name.
        /// </summary>
        internal const string MongoDbCollection = "mongodb.collection";

        /// <summary>
        /// The operation name of the GraphQL request.
        /// </summary>
        internal const string GraphQLOperationName = "graphql.operation.name";

        /// <summary>
        /// The operation type of the GraphQL request.
        /// </summary>
        internal const string GraphQLOperationType = "graphql.operation.type";

        /// <summary>
        /// The source defining the GraphQL request.
        /// </summary>
        internal const string GraphQLSource = "graphql.source";

        /// <summary>
        /// The message source name.
        /// </summary>
        internal const string MessagingSourceName = "messaging.source.name";

        /// <summary>
        /// The message destination name.
        /// </summary>
        internal const string MessagingDestinationName = "messaging.destination.name";

        /// <summary>
        /// The message destination name, using legacy naming.
        /// </summary>
        internal const string LegacyMessageBusDestination = "message_bus.destination";

        /// <summary>
        /// The messaging operation.
        /// </summary>
        internal const string MessagingOperation = "messaging.operation";

        /// <summary>
        /// The AMQP method.
        /// </summary>
        internal const string AmqpCommand = "amqp.command";

        /// <summary>
        /// The name of the AMQP exchange the message was originally published to.
        /// </summary>
        internal const string AmqpExchange = "amqp.exchange";

        /// <summary>
        /// The routing key for the AMQP message.
        /// </summary>
        internal const string AmqpRoutingKey = "amqp.routing_key";

        /// <summary>
        /// The name of the queue for the AMQP message.
        /// </summary>
        internal const string AmqpQueue = "amqp.queue";

        /// <summary>
        /// The delivery mode of the AMQP message.
        /// </summary>
        internal const string AmqpDeliveryMode = "amqp.delivery_mode";

        /// <summary>
        /// The bootstrap servers as defined in producer or consumer config
        /// </summary>
        internal const string KafkaBootstrapServers = "messaging.kafka.bootstrap.servers";

        /// <summary>
        /// The partition associated with a record
        /// </summary>
        internal const string KafkaPartition = "kafka.partition";

        /// <summary>
        /// The offset inside a partition associated with a record
        /// </summary>
        internal const string KafkaOffset = "kafka.offset";

        /// <summary>
        /// The consumer group that consumed the message
        /// </summary>
        internal const string KafkaConsumerGroup = "kafka.group";

        /// <summary>
        /// Whether the record was a "tombstone" record
        /// </summary>
        internal const string KafkaTombstone = "kafka.tombstone";

        /// <summary>
        /// The agent that instrumented the associated AWS SDK span.
        /// </summary>
        internal const string AwsAgentName = "aws.agent";

        /// <summary>
        /// The operation associated with the AWS SDK span.
        /// </summary>
        internal const string AwsOperationName = "aws.operation";

        /// <summary>
        /// The region associated with the AWS SDK span.
        /// </summary>
        [Obsolete("AwsRegion is a duplicate. Use Region instead.")]
        internal const string AwsRegion = "aws.region";

        /// <summary>
        /// The region associated with the span.
        /// </summary>
        internal const string Region = "region";

        /// <summary>
        /// The request ID associated with the AWS SDK span.
        /// </summary>
        internal const string AwsRequestId = "aws.requestId";

        /// <summary>
        /// The service associated with the AWS SDK span.
        /// </summary>
        [Obsolete("AwsServiceName is a duplicate. Use AwsService instead.")]
        internal const string AwsServiceName = "aws.service";

        /// <summary>
        /// The AWS service associated with the AWS SDK span.
        /// </summary>
        internal const string AwsService = "aws_service";

        /// <summary>
        /// The queue name associated with the AWS SDK span.
        /// </summary>
        [Obsolete("AwsTopicName is a duplicate. Use TopicName instead.")]
        internal const string AwsTopicName = "aws.topic.name";

        /// <summary>
        /// The topic name associated with the AWS SDK SNS span.
        /// </summary>
        internal const string TopicName = "topicname";

        /// <summary>
        /// The queue name associated with the AWS SDK SQS span.
        /// </summary>
        [Obsolete("AwsQueueName is a duplicate. Use QueueName instead.")]
        internal const string AwsQueueName = "aws.queue.name";

        /// <summary>
        /// The queue name associated with the span.
        /// </summary>
        internal const string QueueName = "queuename";

        /// <summary>
        /// The topic arn associated with the AWS SDK span.
        /// </summary>
        internal const string AwsTopicArn = "aws.topic.arn";

        /// <summary>
        /// The queue URL associated with the AWS SDK span.
        /// </summary>
        internal const string AwsQueueUrl = "aws.queue.url";

        /// <summary>
        /// The stream name associated with the AWS SDK Kinesis span.
        /// </summary>
        internal const string StreamName = "streamname";

        /// <summary>
        /// The table name associated with the AWS SDK DynamoDB span.
        /// </summary>
        internal const string TableName = "tablename";

        /// <summary>
        /// Configures Trace Analytics.
        /// </summary>
        internal const string Analytics = "_dd1.sr.eausr";

        /// <summary>
        /// The runtime family tag, it will be placed on the service entry span, the first span opened for a
        /// service. For this library it will always have the value "dotnet".
        /// </summary>
        internal const string RuntimeFamily = "_dd.runtime_family";

        /// <summary>
        /// The resource ID of the site instance in Azure App Services where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesResourceId = "aas.resource.id";

        /// <summary>
        /// The resource group of the site instance in Azure App Services where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesResourceGroup = "aas.resource.group";

        /// <summary>
        /// The site name of the site instance in Azure where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesSiteName = "aas.site.name";

        /// <summary>
        /// The version of the extension installed where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesExtensionVersion = "aas.environment.extension_version";

        /// <summary>
        /// The instance name in Azure where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesInstanceName = "aas.environment.instance_name";

        /// <summary>
        /// The instance ID in Azure where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesInstanceId = "aas.environment.instance_id";

        /// <summary>
        /// The operating system in Azure where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesOperatingSystem = "aas.environment.os";

        /// <summary>
        /// The runtime in Azure where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesRuntime = "aas.environment.runtime";

        /// <summary>
        /// The kind of application instance running in Azure.
        /// Possible values: app, api, mobileapp, app_linux, app_linux_container, functionapp, functionapp_linux, functionapp_linux_container
        /// </summary>
        internal const string AzureAppServicesSiteKind = "aas.site.kind";

        /// <summary>
        /// The type of application instance running in Azure.
        /// Possible values: app, function
        /// </summary>
        internal const string AzureAppServicesSiteType = "aas.site.type";

        /// <summary>
        /// The subscription ID of the site instance in Azure App Services where the traced application is running.
        /// </summary>
        internal const string AzureAppServicesSubscriptionId = "aas.subscription.id";

        /// <summary>
        /// The type of trigger for an azure function
        /// </summary>
        internal const string AzureFunctionTriggerType = "aas.function.trigger";

        /// <summary>
        /// The UI name of the azure function
        /// </summary>
        internal const string AzureFunctionName = "aas.function.name";

        /// <summary>
        /// The full method name of the azure function
        /// </summary>
        internal const string AzureFunctionMethod = "aas.function.method";

        /// <summary>
        /// The literal type of the binding for the azure function trigger
        /// </summary>
        internal const string AzureFunctionBindingSource = "aas.function.binding";

        /// <summary>
        /// Configures the origin of the trace. This tag is added during MessagePack serialization
        /// using the value from <see cref="TraceContext.Origin"/>.
        /// </summary>
        internal const string Origin = "_dd.origin";

        /// <summary>
        /// Configures the measured metric for a span.
        /// </summary>
        internal const string Measured = "_dd.measured";

        /// <summary>
        /// Marks a span as a partial snapshot.
        /// </summary>
        internal const string PartialSnapshot = "_dd.partial_version";

        /// <summary>
        /// The name of the Msmq command the message was published to.
        /// </summary>
        internal const string MsmqCommand = "msmq.command";

        /// <summary>
        /// Is the msmq queue supporting transactional messages
        /// </summary>
        internal const string MsmqIsTransactionalQueue = "msmq.queue.transactional";

        /// <summary>
        /// The name of the Msmq queue the message was published to, containing host name and path.
        /// </summary>
        internal const string MsmqQueuePath = "msmq.queue.path";

        /// <summary>
        /// A boolean indicating if it's part of a transaction.
        /// </summary>
        internal const string MsmqMessageWithTransaction = "msmq.message.transactional";

        /// <summary>
        /// A CosmosDb container name.
        /// </summary>
        internal const string CosmosDbContainer = "cosmosdb.container";

        /// <summary>
        /// If a span was involved with an application security event
        /// </summary>
        internal const string AppSecEvent = "appsec.event";

        /// <summary>
        /// If a span was involved with an application security event and that the request was blocked
        /// </summary>
        internal const string AppSecBlocked = "appsec.blocked";

        /// <summary>
        /// The details of the security event
        /// </summary>
        internal const string AppSecJson = "_dd.appsec.json";

        /// <summary>
        /// Ruleset file version, string satisfying the regular expression: [0-9]+\.[0-9]+\.[0-9]+
        /// </summary>
        internal const string AppSecRuleFileVersion = "_dd.appsec.event_rules.version";

        /// <summary>
        /// Version of the waf
        /// </summary>
        internal const string AppSecWafVersion = "_dd.appsec.waf.version";

        /// <summary>
        ///  String-serialized JSON array, each item being a map containing:
        ///  Error(e) - the error string.
        ///  Rules(r) - an array of rules which failed to load with this error.
        /// </summary>
        internal const string AppSecWafInitRuleErrors = "_dd.appsec.event_rules.errors";

        /// <summary>
        /// Should contain the public IP of the host initiating the request.
        /// </summary>
        internal const string ActorIp = "actor.ip";

        /// <summary>
        /// Should contain the vulnerability json
        /// </summary>
        internal const string IastJson = "_dd.iast.json";

        /// <summary>
        /// Indicates if the vulnerability json has been truncated because it exceeds the maximum tag size
        /// </summary>
        internal const string IastJsonTagSizeExceeded = "_dd.iast.json.tag.size.exceeded";

        /// <summary>
        /// Indicates at the end of a request if IAST analisys has been performned
        /// </summary>
        internal const string IastEnabled = "_dd.iast.enabled";

        /// <summary>
        /// The ip as reported by the framework.
        /// </summary>
        internal const string NetworkClientIp = "network.client.ip";

        internal const string ElasticsearchAction = "elasticsearch.action";

        internal const string ElasticsearchMethod = "elasticsearch.method";

        internal const string ElasticsearchUrl = "elasticsearch.url";

        internal const string RuntimeId = "runtime-id";

        internal const string AerospikeKey = "aerospike.key";

        internal const string AerospikeNamespace = "aerospike.namespace";

        internal const string AerospikeSetName = "aerospike.setname";

        internal const string AerospikeUserKey = "aerospike.userkey";

        internal const string CouchbaseSeedNodes = "db.couchbase.seed.nodes";
        internal const string CouchbaseOperationCode = "couchbase.operation.code";
        internal const string CouchbaseOperationBucket = "couchbase.operation.bucket";
        internal const string CouchbaseOperationKey = "couchbase.operation.key";

        internal const string RpcMethod = "rpc.method";
        internal const string RpcService = "rpc.service";
        internal const string RpcSystem = "rpc.system";

        internal const string GrpcMethodKind = "grpc.method.kind";
        internal const string GrpcMethodPath = "grpc.method.path";
        internal const string GrpcMethodPackage = "grpc.method.package";
        internal const string GrpcMethodService = "grpc.method.service";
        internal const string GrpcMethodName = "grpc.method.name";
        internal const string GrpcStatusCode = "grpc.status.code";

        // general Service Fabric
        internal const string ServiceFabricApplicationId = "service-fabric.application-id";
        internal const string ServiceFabricApplicationName = "service-fabric.application-name";
        internal const string ServiceFabricPartitionId = "service-fabric.partition-id";
        internal const string ServiceFabricNodeId = "service-fabric.node-id";
        internal const string ServiceFabricNodeName = "service-fabric.node-name";
        internal const string ServiceFabricServiceName = "service-fabric.service-name";

        // Service Remoting
        internal const string ServiceRemotingUri = "service-fabric.service-remoting.uri";
        internal const string ServiceRemotingServiceName = "service-fabric.service-remoting.service";
        internal const string ServiceRemotingMethodName = "service-fabric.service-remoting.method-name";
        internal const string ServiceRemotingMethodId = "service-fabric.service-remoting.method-id";
        internal const string ServiceRemotingInterfaceId = "service-fabric.service-remoting.interface-id";
        internal const string ServiceRemotingInvocationId = "service-fabric.service-remoting.invocation-id";

        internal const string ProcessEnvironmentVariables = "cmd.environment_variables";
        internal const string ProcessComponent = "cmd.component";
        internal const string ProcessCommandExec = "cmd.exec";
        internal const string ProcessCommandShell = "cmd.shell";
        internal const string ProcessTruncated = "cmd.truncated";

        internal const string TagPropagationError = "_dd.propagation_error";

        /// <summary>
        /// Marks a span as injected when DBM comment has the traceParent on it
        /// </summary>
        internal const string DbmTraceInjected = "_dd.dbm_trace_injected";

        /// <summary>
        /// Holds the original value for Service when Service is overriden after span creation
        /// </summary>
        internal const string BaseService = "_dd.base_service";

        /// <summary>
        /// Tag used to propagate the unsigned  64 bits last parent Id
        /// lower-case 16 characters hexadecimal string
        /// </summary>
        internal const string LastParentId = "_dd.parent_id";

        internal static class User
        {
            internal const string Email = "usr.email";
            internal const string Name = "usr.name";
            internal const string Id = "usr.id";
            internal const string SessionId = "usr.session_id";
            internal const string Role = "usr.role";
            internal const string Scope = "usr.scope";
        }

        internal static class Propagated
        {
            /// <summary>
            /// Tag used to propagate the sampling mechanism used to make a sampling decision.
            /// See <see cref="Datadog.Trace.Sampling.SamplingMechanism"/>.
            /// </summary>
            /// <remarks>
            /// This tag was originally meant to carry the name of the service that made the sampling decision,
            /// hence its name: "decision maker".
            /// </remarks>
            internal const string DecisionMaker = "_dd.p.dm";

            /// <summary>
            /// Tag used to propagate the higher-order 64 bits of a 128-bit trace id encoded as a
            /// lower-case hexadecimal string with no zero-padding or `0x` prefix.
            /// </summary>
            internal const string TraceIdUpper = "_dd.p.tid";

            /// <summary>
            /// A boolean allowing the propagation to downstream services the information that the current distributed trace
            /// is containing at least one ASM security event, no matter its type (threats, business logic events, IAST, etc.).
            /// </summary>
            internal const string AppSec = "_dd.p.appsec";
        }
    }
}
