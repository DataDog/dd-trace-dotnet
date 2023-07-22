// <copyright file="SpanMetadataV0Rules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    internal static class SpanMetadataV0Rules
    {
        public static Result IsAdoNetV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsPresent("db.type")
                .Matches("component", "AdoNet")
                .Matches("span.kind", "client"));

        public static Result IsAerospikeV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aerospike.command")
                .Matches(Type, "aerospike"))
            .Tags(s => s
                .IsOptional("aerospike.key")
                .IsOptional("aerospike.namespace")
                .IsOptional("aerospike.setname")
                .IsOptional("aerospike.userkey")
                .Matches("component", "aerospike")
                .Matches("span.kind", "client"));

        public static Result IsAspNetV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aspnet.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsOptional("http.client_ip")
                .IsOptional("network.client.ip")
                .IsPresent("http.method")
                .IsPresent("http.request.headers.host")
                .IsOptional("http.route")
                .IsPresent("http.status_code")
                .IsPresent("http.useragent")
                .IsPresent("http.url")
                // BUG: component tag is not set
                // .Matches("component", "aspnet")
                .Matches("span.kind", "server"));

        public static Result IsAspNetMvcV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aspnet-mvc.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsPresent("aspnet.action")
                .IsOptional("aspnet.area")
                .IsPresent("aspnet.controller")
                .IsPresent("aspnet.route")
                .IsPresent("http.method")
                .IsPresent("http.request.headers.host")
                .IsPresent("http.status_code")
                .IsPresent("http.useragent")
                .IsPresent("http.url")
                // BUG: component tag is not set
                // .Matches("component", "aspnet")
                .Matches("span.kind", "server"));

        public static Result IsAspNetWebApi2V0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aspnet-webapi.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsOptional("aspnet.action")
                .IsOptional("aspnet.controller")
                .IsPresent("aspnet.route")
                .IsOptional("http.client_ip")
                .IsOptional("network.client.ip")
                .IsPresent("http.method")
                .IsPresent("http.request.headers.host")
                .IsOptional("http.route")
                // BUG: When WebApi2 throws an exception, we cannot immediately set the
                // status code because the response hasn't been written yet.
                // For ASP.NET, we register a callback to populate http.status_code
                // when the request has completed, but on OWIN there is no such mechanism.
                // What we should do is instrument OWIN and assert that that has the
                // "http.status_code" tag
                // .IsPresent("http.status_code")
                .IsOptional("http.status_code")
                .IsPresent("http.useragent")
                .IsPresent("http.url")
                // BUG: component tag is not set
                // .Matches("component", "aspnet")
                .Matches("span.kind", "server"));

        public static Result IsAspNetCoreV0(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Name, "aspnet_core.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsOptional("aspnet_core.endpoint")
                .IsOptional("aspnet_core.route")
                .IsOptional("http.client_ip")
                .IsOptional("network.client.ip")
                .IsPresent("http.method")
                .IsPresent("http.request.headers.host")
                .IsOptional("http.route")
                .IsPresent("http.status_code")
                .IsPresent("http.useragent")
                .IsPresent("http.url")
                .Matches("component", "aspnet_core")
                .Matches("span.kind", "server"));

        public static Result IsAspNetCoreMvcV0(this MockSpan span) => Result.FromSpan(span)
            .WithIntegrationName("AspNetCore")
            .Properties(s => s
                .Matches(Name, "aspnet_core_mvc.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsPresent("aspnet_core.action")
                .IsOptional("aspnet_core.area")
                .IsPresent("aspnet_core.controller")
                .IsOptional("aspnet_core.page")
                .IsPresent("aspnet_core.route")
                .Matches("component", "aspnet_core")
                .Matches("span.kind", "server"));

        public static Result IsAwsSqsRequestV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "sqs.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SQS")
                .IsOptional("aws.queue.name")
                .IsOptional("aws.queue.url")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "client"));

        public static Result IsAwsSnsV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "sns.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SNS")
                .IsOptional("aws.topic.name")
                .IsOptional("aws.topic.arn")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "client"));

        public static Result IsCosmosDbV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "cosmosdb.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("cosmosdb.container")
                .IsOptional("db.name")
                .Matches("db.type", "cosmosdb")
                .IsPresent("out.host")
                .Matches("component", "CosmosDb")
                .Matches("span.kind", "client"));

        public static Result IsCouchbaseV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "couchbase.query")
                .Matches(Type, "db"))
            .Tags(s => s
                .IsPresent("db.couchbase.seed.nodes")
                .IsOptional("couchbase.operation.bucket")
                .IsPresent("couchbase.operation.code")
                .IsPresent("couchbase.operation.key")
                .IsOptional("out.port")
                .IsOptional("out.host")
                .Matches("component", "Couchbase")
                .Matches("span.kind", "client"));

        public static Result IsElasticsearchNetV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "elasticsearch.query")
                .Matches(Type, "elasticsearch"))
            .Tags(s => s
                .IsPresent("elasticsearch.action")
                .IsPresent("elasticsearch.method")
                .IsPresent("elasticsearch.url")
                .IsPresent("out.host")
                .Matches("component", "elasticsearch-net")
                .Matches("span.kind", "client"));

        public static Result IsGraphQLV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "graphql.execute", "graphql.validate")
                .Matches(Type, "graphql"))
            .Tags(s => s
                .IsOptional("graphql.operation.name")
                .IsOptional("graphql.operation.type")
                .IsPresent("graphql.source")
                .Matches("component", "GraphQL")
                .Matches("span.kind", "server"));

        public static Result IsGrpcClientV0(this MockSpan span, ISet<string> excludeTags) => Result.FromSpan(span, excludeTags)
            .WithMarkdownSection("gRPC Client")
            .Properties(s => s
                .Matches(Name, "grpc.request")
                .Matches(Type, "grpc"))
            .Tags(s => s
                .IsPresent("grpc.method.kind")
                .IsPresent("grpc.method.name")
                .IsPresent("grpc.method.package")
                .IsPresent("grpc.method.path")
                .IsPresent("grpc.method.service")
                .IsPresent("grpc.status.code")
                .IsPresent("out.host")
                .Matches("component", "Grpc")
                .Matches("span.kind", "client"));

        public static Result IsGrpcServerV0(this MockSpan span, ISet<string> excludeTags) => Result.FromSpan(span, excludeTags)
            .WithMarkdownSection("gRPC Server")
            .Properties(s => s
                .Matches(Name, "grpc.request")
                .Matches(Type, "grpc"))
            .Tags(s => s
                .IsPresent("grpc.method.kind")
                .IsPresent("grpc.method.name")
                .IsPresent("grpc.method.package")
                .IsPresent("grpc.method.path")
                .IsPresent("grpc.method.service")
                .IsPresent("grpc.status.code")
                .Matches("component", "Grpc")
                .Matches("span.kind", "server"));

        public static Result IsHotChocolateV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "graphql.execute", "graphql.validate")
                .Matches(Type, "graphql"))
            .Tags(s => s
                .IsOptional("graphql.operation.name")
                .IsOptional("graphql.operation.type")
                .IsPresent("graphql.source")
                .Matches("component", "HotChocolate")
                .Matches("span.kind", "server"));

        public static Result IsHttpMessageHandlerV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .IsPresent("http-client-handler-type")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("out.host")
                .IsPresent("component")
                .Matches("span.kind", "client"));

        public static Result IsKafkaInboundV0(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Kafka - Inbound")
            .Properties(s => s
                .Matches(Name, "kafka.consume")
                .Matches(Type, "queue"))
            .Metrics(s => s
                .IsPresent("_dd.measured")
                .IsOptional("message.queue_time_ms"))
            .Tags(s => s
                .IsOptional("kafka.group")
                .IsOptional("kafka.offset")
                .IsOptional("kafka.partition")
                .IsOptional("kafka.tombstone")
                .IsPresent("messaging.kafka.bootstrap.servers")
                .Matches("component", "kafka")
                .Matches("span.kind", "consumer"));

        public static Result IsKafkaOutboundV0(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Kafka - Outbound")
            .Properties(s => s
                .Matches(Name, "kafka.produce")
                .Matches(Type, "queue"))
            .Metrics(s => s
                .IsPresent("_dd.measured")
                .IsOptional("message.queue_time_ms"))
            .Tags(s => s
                .IsOptional("kafka.group")
                .IsOptional("kafka.offset")
                .IsOptional("kafka.partition")
                .IsOptional("kafka.tombstone")
                .IsPresent("messaging.kafka.bootstrap.servers")
                .Matches("component", "kafka")
                .Matches("span.kind", "producer"));

        public static Result IsMongoDbV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "mongodb.query")
                .Matches(Type, "mongodb"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsOptional("mongodb.collection")
                .IsOptional("mongodb.query")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .Matches("component", "MongoDb")
                .Matches("span.kind", "client"));

        public static Result IsMsmqV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "msmq.command")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("msmq.command")
                .IsOptional("msmq.message.transactional")
                .IsPresent("msmq.queue.path")
                .IsOptional("msmq.queue.transactional")
                .IsPresent("out.host")
                .Matches("component", "msmq")
                .MatchesOneOf("span.kind", "client", "producer", "consumer"));

        public static Result IsMySqlV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "mysql.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsPresent("db.name")
                .IsPresent("db.user")
                .IsPresent("out.host")
                .Matches("db.type", "mysql")
                .Matches("component", "MySql")
                .Matches("span.kind", "client"));

        public static Result IsNpgsqlV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "postgres.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsPresent("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "postgres")
                .Matches("component", "Npgsql")
                .Matches("span.kind", "client"));

        public static Result IsOpenTelemetryV0(this MockSpan span, ISet<string> resources, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => { })
            .AdditionalTags(s => s
                .PassesThroughSource("OTEL Resource Attributes", resources))
            .Tags(s => s
                // .IsOptional("events") // aka span events, added by the trace agent when the OTLP span is populated with events
                .IsPresent("otel.library.name")
                .IsOptional("otel.library.version")
                .IsPresent("otel.trace_id")
                .MatchesOneOf("otel.status_code", "STATUS_CODE_UNSET", "STATUS_CODE_OK", "STATUS_CODE_ERROR")
                .IsOptional("otel.status_description")
                .MatchesOneOf("span.kind", "internal", "server", "client", "producer", "consumer"));

        public static Result IsOracleV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "oracle.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsPresent("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "oracle")
                .Matches("component", "Oracle")
                .Matches("span.kind", "client"));

        public static Result IsProcessV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "command_execution")
                .Matches(Type, "system"))
            .Tags(s => s
                .IsOptional("cmd.environment_variables")
                .Matches("span.kind", "internal"));

        public static Result IsRabbitMQV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "amqp.command")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("amqp.command")
                .IsOptional("amqp.delivery_mode")
                .IsOptional("amqp.exchange")
                .IsOptional("amqp.routing_key")
                .IsOptional("amqp.queue")
                .IsOptional("message.size")
                .Matches("component", "RabbitMQ")
                .IsPresent("span.kind"));

        public static Result IsServiceRemotingV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "service_remoting.client", "service_remoting.server"))
            .Tags(s => s
                .IsOptional("service-fabric.application-id")
                .IsOptional("service-fabric.application-name")
                .IsOptional("service-fabric.partition-id")
                .IsOptional("service-fabric.node-id")
                .IsOptional("service-fabric.node-name")
                .IsOptional("service-fabric.service-name")
                .IsPresent("service-fabric.service-remoting.uri")
                .IsPresent("service-fabric.service-remoting.method-name")
                .IsOptional("service-fabric.service-remoting.method-id")
                .IsOptional("service-fabric.service-remoting.interface-id")
                .IsOptional("service-fabric.service-remoting.invocation-id")
                .MatchesOneOf("span.kind", "client", "server"));

        public static Result IsServiceStackRedisV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "redis.command")
                .Matches(Type, "redis"))
            .Metrics(s => s
                .IsPresent("db.redis.database_index"))
            .Tags(s => s
                .IsPresent("redis.raw_command")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .Matches("component", "ServiceStackRedis")
                .Matches("span.kind", "client"));

        public static Result IsStackExchangeRedisV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "redis.command")
                .Matches(Type, "redis"))
            .Metrics(s => s
                .IsOptional("db.redis.database_index"))
            .Tags(s => s
                .IsPresent("redis.raw_command")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .Matches("component", "StackExchangeRedis")
                .Matches("span.kind", "client"));

        public static Result IsSqliteV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "sqlite.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "sqlite")
                .Matches("component", "Sqlite")
                .Matches("span.kind", "client"));

        public static Result IsSqlClientV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "sql-server.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "sql-server")
                .Matches("component", "SqlClient")
                .Matches("span.kind", "client")
                .IsOptional("_dd.dbm_trace_injected"));

        public static Result IsWcfV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "wcf.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsOptional("http.method")
                .IsOptional("http.request.headers.host")
                .IsPresent("http.url")
                .Matches("component", "Wcf")
                .Matches("span.kind", "server"));

        public static Result IsWebRequestV0(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .IsOptional("http-client-handler-type")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("out.host")
                .MatchesOneOf("component", "HttpMessageHandler", "WebRequest")
                .Matches("span.kind", "client"));
    }
}
