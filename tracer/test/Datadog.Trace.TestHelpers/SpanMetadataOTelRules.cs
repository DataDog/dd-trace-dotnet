// <copyright file="SpanMetadataOTelRules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    internal static class SpanMetadataOTelRules
    {
        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/
        public static Result IsHttpClientRequestOTel(this MockSpan span) => Result.FromSpan(span)
            .Tags(s => s
                // Required
                .IsPresent("http.request.method")
                .IsPresent("server.address")
                .IsPresent("url.full")
                // Conditionally required
                .IsOptional("error.type")
                .IsOptional("http.request.method_original")
                .IsOptional("http.response.status_code")
                .IsOptional("network.protocol.name")
                .IsOptional("server.port")
                // Recommended
                .IsOptional("http.request.resend_count")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.version")
                // DD Only
                .IsOptional("span.kind")
                .IsOptional("component")
                .IsOptional("http-client-handler-type")
                .IsOptional("_dd.base_service")
                .IsOptional("_dd.tags.process")
                .IsOptional("_dd.svc_src"));

        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/
        public static Result IsHttpServerRequestOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Tags(s => s
                // Required
                .IsPresent("http.request.method")
                .IsPresent("url.path")
                .IsPresent("url.scheme")
                // Conditionally required
                .IsOptional("error.type")
                .IsOptional("http.request.method_original")
                .IsOptional("http.response.status_code")
                .IsOptional("http.route")
                .IsOptional("network.protocol.name")
                .IsOptional("server.port")
                .IsOptional("url.query")
                // Recommended
                .IsOptional("client.address")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.version")
                .IsOptional("server.address")
                .IsOptional("user_agent.original")
                // DD Only
                .IsOptional("_dd.base_service")
                .IsOptional("_dd.tags.process"));

        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/
        public static Result IsAspNetCoreOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Tags(s => s
                // Required
                .IsPresent("http.request.method")
                .IsPresent("url.path")
                .IsPresent("url.scheme")
                // Conditionally required
                .IsOptional("error.type")
                .IsOptional("http.request.method_original")
                .IsOptional("http.response.status_code")
                .IsOptional("http.route")
                .IsOptional("network.protocol.name")
                .IsOptional("server.port")
                .IsOptional("url.query")
                // Recommended
                .IsOptional("client.address")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.version")
                .IsOptional("server.address")
                .IsOptional("user_agent.original")
                // ASP.NET Core specific
                // .IsOptional("aspnet_core.endpoint")
                // .IsOptional("aspnet_core.route"));
                // DD Only
                .IsOptional("_dd.base_service")
                .IsOptional("_dd.tags.process"));

        // See: https://opentelemetry.io/docs/specs/semconv/http/http-spans/
        public static Result IsAspNetCoreMvcOTel(this MockSpan span) => Result.FromSpan(span)
            .WithIntegrationName("AspNetCore")
            .Properties(s => s
                .Matches(Type, "web"))
            .Tags(s => s
                // Required
                .IsPresent("http.request.method")
                .IsPresent("url.path")
                .IsPresent("url.scheme")
                // Conditionally required
                .IsOptional("error.type")
                .IsOptional("http.request.method_original")
                .IsOptional("http.response.status_code")
                .IsOptional("http.route")
                .IsOptional("network.protocol.name")
                .IsOptional("server.port")
                .IsOptional("url.query")
                // Recommended
                .IsOptional("client.address")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("network.protocol.version")
                .IsOptional("server.address")
                .IsOptional("user_agent.original"));
                // ASP.NET Core MVC specific
                // .IsPresent("aspnet_core.action")
                // .IsOptional("aspnet_core.area")
                // .IsPresent("aspnet_core.controller")
                // .IsOptional("aspnet_core.page")
                // .IsPresent("aspnet_core.route")

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsDatabaseClientOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .IsPresent("db.system")
                // Conditionally required
                .IsOptional("db.collection.name")
                .IsOptional("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("db.response.status_code")
                .IsOptional("error.type")
                .IsOptional("server.port")
                // Recommended
                .IsOptional("db.query.summary")
                .IsOptional("db.query.text")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                .IsOptional("server.address")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsSqlClientOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .Matches("db.system", "mssql")
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("db.collection.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("db.query.text")
                .IsOptional("db.user")
                .IsOptional("network.peer.address")
                .IsOptional("dd.instrumentation.time_ms")
                .Matches("component", "SqlClient")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsMySqlOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .Matches("db.system", "mysql")
                // Conditionally required
                .IsPresent("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("db.collection.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("db.query.text")
                .IsOptional("db.user")
                .IsOptional("network.peer.address")
                .Matches("component", "MySql")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsNpgsqlOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .Matches("db.system", "postgresql")
                // Conditionally required
                .IsPresent("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("db.collection.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("db.query.text")
                .IsOptional("db.user")
                .IsOptional("network.peer.address")
                .Matches("component", "Npgsql")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsSqliteOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .Matches("db.system", "sqlite")
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("db.collection.name")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("server.address")
                .IsOptional("server.port")
                .IsOptional("db.query.text")
                .Matches("component", "Sqlite")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsOracleOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .Matches("db.system", "oracle")
                // Conditionally required
                .IsPresent("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("db.collection.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("db.query.text")
                .IsOptional("network.peer.address")
                .Matches("component", "Oracle")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsMongoDbOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "mongodb"))
            .Tags(s => s
                // Required
                .Matches("db.system", "mongodb")
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.collection.name")
                .IsOptional("db.operation.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsPresent("server.port")
                .IsOptional("db.query.text")
                .IsOptional("network.peer.address")
                .Matches("component", "MongoDb")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsServiceStackRedisOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "redis"))
            .Metrics(s => s
                .IsPresent("db.redis.database_index"))
            .Tags(s => s
                // Required
                .Matches("db.system", "redis")
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsPresent("server.port")
                .IsOptional("db.query.text")
                .IsOptional("network.peer.address")
                .Matches("component", "ServiceStackRedis")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsStackExchangeRedisOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "redis"))
            .Metrics(s => s
                .IsOptional("db.redis.database_index"))
            .Tags(s => s
                // Required
                .Matches("db.system", "redis")
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.operation.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsPresent("server.port")
                .IsOptional("db.query.text")
                .IsOptional("network.peer.address")
                .Matches("component", "StackExchangeRedis")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsElasticsearchOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "elasticsearch"))
            .Tags(s => s
                // Required
                .Matches("db.system", "elasticsearch")
                // Conditionally required
                .IsOptional("db.operation.name")
                .IsOptional("db.collection.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("db.query.text")
                .IsOptional("network.peer.address")
                // Elasticsearch-specific
                .IsPresent("elasticsearch.action")
                .IsPresent("elasticsearch.method")
                .IsPresent("elasticsearch.url")
                .Matches("component", "elasticsearch-net")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
        public static Result IsCosmosDbOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                // Required
                .Matches("db.system", "cosmosdb")
                // Conditionally required
                .IsOptional("db.namespace")
                .IsOptional("db.collection.name")
                .IsOptional("db.operation.name")
                .IsOptional("db.response.status_code")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("network.peer.address")
                // CosmosDB-specific
                .IsOptional("db.cosmosdb.connection_mode")
                .IsOptional("db.cosmosdb.response.sub_status_code")
                .IsOptional("user_agent.original")
                .Matches("component", "CosmosDb")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsKafkaInboundOTel(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Kafka - Inbound")
            .Properties(s => s
                .Matches(Type, "queue"))
            .Metrics(s => s
                .IsOptional("message.queue_time_ms"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "kafka")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsPresent("messaging.destination.name")
                .IsOptional("messaging.consumer.group.name")
                .IsOptional("messaging.destination.partition.id")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("network.peer.address")
                .IsOptional("server.address")
                .IsOptional("server.port")
                // Kafka-specific
                .IsOptional("messaging.kafka.message.offset")
                .IsOptional("messaging.kafka.message.tombstone")
                .Matches("component", "kafka")
                .Matches("span.kind", "consumer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsKafkaOutboundOTel(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Kafka - Outbound")
            .Properties(s => s
                .Matches(Type, "queue"))
            .Metrics(s => s
                .IsOptional("message.queue_time_ms"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "kafka")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsPresent("messaging.destination.name")
                .IsOptional("messaging.destination.partition.id")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("network.peer.address")
                .IsOptional("server.address")
                .IsOptional("server.port")
                // Kafka-specific
                .IsOptional("messaging.kafka.message.offset")
                .IsOptional("messaging.kafka.message.tombstone")
                .Matches("component", "kafka")
                .Matches("span.kind", "producer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsRabbitMQInboundOTel(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("RabbitMQ - Inbound")
            .Properties(s => s
                .Matches(Type, "queue"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "rabbitmq")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsOptional("messaging.destination.name")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("network.peer.address")
                .IsOptional("server.address")
                .IsOptional("server.port")
                // RabbitMQ-specific
                .IsOptional("messaging.rabbitmq.destination.routing_key")
                .IsOptional("messaging.rabbitmq.message.delivery_tag")
                .Matches("component", "RabbitMQ")
                .Matches("span.kind", "consumer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsRabbitMQOutboundOTel(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("RabbitMQ - Outbound")
            .Properties(s => s
                .Matches(Type, "queue"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "rabbitmq")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsOptional("messaging.destination.name")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("network.peer.address")
                .IsOptional("server.address")
                .IsOptional("server.port")
                // RabbitMQ-specific
                .IsOptional("messaging.rabbitmq.destination.routing_key")
                .Matches("component", "RabbitMQ")
                .Matches("span.kind", "producer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsAwsSqsInboundOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "http"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "aws_sqs")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsPresent("messaging.destination.name")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("messaging.message.id")
                .IsOptional("server.address")
                .IsOptional("server.port")
                // AWS-specific
                .IsOptional("aws.request_id")
                .IsOptional("aws.region")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "consumer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsAwsSqsOutboundOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "http"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "aws_sqs")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsPresent("messaging.destination.name")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("messaging.message.id")
                .IsPresent("server.address")
                .IsOptional("server.port")
                // AWS-specific
                .IsOptional("aws.request_id")
                .IsOptional("aws.region")
                .Matches("component", "aws-sdk")
                .MatchesOneOf("span.kind", "producer", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsAzureServiceBusInboundOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Type, "queue"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "servicebus")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsOptional("messaging.destination.name")
                .IsOptional("messaging.consumer.group.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("messaging.batch.message_count")
                .Matches("component", "AzureServiceBus")
                .Matches("span.kind", "consumer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsAzureServiceBusOutboundOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Type, "queue"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "servicebus")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsPresent("messaging.destination.name")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("messaging.batch.message_count")
                .Matches("component", "AzureServiceBus")
                .Matches("span.kind", "producer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsAzureEventHubsInboundOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Type, "queue"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "eventhubs")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsOptional("messaging.destination.name")
                .IsOptional("messaging.destination.partition.id")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("messaging.batch.message_count")
                .Matches("component", "AzureEventHubs")
                .Matches("span.kind", "consumer"));

        // See: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/
        public static Result IsAzureEventHubsOutboundOTel(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Type, "queue"))
            .Tags(s => s
                // Required
                .Matches("messaging.system", "eventhubs")
                .IsPresent("messaging.operation.name")
                // Conditionally required
                .IsPresent("messaging.destination.name")
                .IsOptional("messaging.destination.partition.id")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("messaging.client.id")
                .IsOptional("messaging.message.id")
                .IsOptional("messaging.batch.message_count")
                .Matches("component", "AzureEventHubs")
                .MatchesOneOf("span.kind", "producer", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/rpc/rpc-spans/
        public static Result IsGrpcClientOTel(this MockSpan span, ISet<string> excludeTags) => Result.FromSpan(span, excludeTags)
            .WithMarkdownSection("gRPC Client")
            .Properties(s => s
                .Matches(Type, "grpc"))
            .Tags(s => s
                // Required
                .Matches("rpc.system", "grpc")
                // Conditionally required
                .IsOptional("rpc.method")
                .IsOptional("rpc.service")
                .IsOptional("error.type")
                // Recommended
                .IsPresent("server.address")
                .IsOptional("server.port")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                // gRPC-specific
                .IsPresent("rpc.grpc.status_code")
                .Matches("component", "Grpc")
                .Matches("span.kind", "client"));

        // See: https://opentelemetry.io/docs/specs/semconv/rpc/rpc-spans/
        public static Result IsGrpcServerOTel(this MockSpan span, ISet<string> excludeTags) => Result.FromSpan(span, excludeTags)
            .WithMarkdownSection("gRPC Server")
            .Properties(s => s
                .Matches(Type, "grpc"))
            .Tags(s => s
                // Required
                .Matches("rpc.system", "grpc")
                // Conditionally required
                .IsOptional("rpc.method")
                .IsOptional("rpc.service")
                .IsOptional("error.type")
                // Recommended
                .IsOptional("server.address")
                .IsOptional("server.port")
                .IsOptional("network.peer.address")
                .IsOptional("network.peer.port")
                // gRPC-specific
                .IsPresent("rpc.grpc.status_code")
                .Matches("component", "Grpc")
                .Matches("span.kind", "server"));

        // See: https://opentelemetry.io/docs/specs/semconv/graphql/graphql-spans/
        public static Result IsGraphQLOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "graphql.execute", "graphql.validate")
                .Matches(Type, "graphql"))
            .Tags(s => s
                .IsOptional("graphql.document")
                .IsOptional("graphql.operation.name")
                .IsOptional("graphql.operation.type")
                .IsPresent("graphql.source")
                .IsOptional("error.type")
                .Matches("component", "GraphQL")
                .Matches("span.kind", "server")
                .IsOptional("events"));

        // See: https://opentelemetry.io/docs/specs/semconv/graphql/graphql-spans/
        public static Result IsHotChocolateOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "graphql.execute", "graphql.validate")
                .Matches(Type, "graphql"))
            .Tags(s => s
                .IsOptional("graphql.document")
                .IsOptional("graphql.operation.name")
                .IsOptional("graphql.operation.type")
                .IsPresent("graphql.source")
                .IsOptional("error.type")
                .Matches("component", "HotChocolate")
                .Matches("span.kind", "server")
                .IsOptional("events"));

        // See: https://opentelemetry.io/docs/specs/semconv/system/process/
        public static Result IsProcessOTel(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "command_execution")
                .Matches(Type, "system"))
            .Tags(s => s
                .IsOptional("process.command")
                .IsOptional("process.command_args")
                .IsOptional("process.command_line")
                .IsOptional("error.type")
                .Matches("component", "process")
                .Matches("span.kind", "internal"));
    }
}
