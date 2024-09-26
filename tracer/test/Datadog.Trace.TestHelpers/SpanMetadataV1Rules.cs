// <copyright file="SpanMetadataV1Rules.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    internal static class SpanMetadataV1Rules
    {
        public static Result IsAdoNetV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsPresent("db.type")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "AdoNet")
                .Matches("span.kind", "client"));

        public static Result IsAerospikeV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aerospike.command")
                .Matches(Type, "aerospike"))
            .Tags(s => s
                .IsOptional("aerospike.key")
                .IsOptional("aerospike.namespace")
                .IsOptional("aerospike.setname")
                .IsOptional("aerospike.userkey")
                .IsOptional("peer.service")
                .IsOptional("_dd.peer.service.source")
                .IsOptional("_dd.base_service")
                .Matches("component", "aerospike")
                .Matches("span.kind", "client"));

        public static Result IsAspNetV1(this MockSpan span) => Result.FromSpan(span)
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
                .IsOptional("_dd.base_service")
                // BUG: component tag is not set
                // .Matches("component", "aspnet")
                .Matches("span.kind", "server"));

        public static Result IsAspNetMvcV1(this MockSpan span) => Result.FromSpan(span)
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
                .IsOptional("_dd.base_service")
                // BUG: component tag is not set
                // .Matches("component", "aspnet")
                .Matches("span.kind", "server"));

        public static Result IsAspNetWebApi2V1(this MockSpan span) => Result.FromSpan(span)
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
                .IsOptional("_dd.base_service")
                // BUG: component tag is not set
                // .Matches("component", "aspnet")
                .Matches("span.kind", "server"));

        public static Result IsAspNetCoreV1(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
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
                .IsOptional("_dd.base_service")
                .Matches("component", "aspnet_core")
                .Matches("span.kind", "server"));

        public static Result IsAspNetCoreMvcV1(this MockSpan span) => Result.FromSpan(span)
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
                .IsOptional("_dd.base_service")
                .Matches("component", "aspnet_core")
                .Matches("span.kind", "server"));

        public static Result IsAwsDynamoDbV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.dynamodb.request")
                .Matches(Type, "dynamodb"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("region")
                .IsOptional("aws.region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "DynamoDB")
                .Matches("aws_service", "DynamoDB")
                .IsPresent("tablename")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "tablename", "peer.service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "client"));

        public static Result IsAwsKinesisOutboundV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.kinesis.send")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("region")
                .IsOptional("aws.region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "Kinesis")
                .Matches("aws_service", "Kinesis")
                .IsPresent("streamname")
                .IsOptional("aws.stream.url")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "streamname", "peer.service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "producer"));

        public static Result IsAwsSqsInboundV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.sqs.process")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsOptional("region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SQS")
                .Matches("aws_service", "SQS")
                .IsPresent("aws.queue.name")
                .IsPresent("queuename")
                .IsOptional("aws.queue.url")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsOptional("_dd.base_service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "consumer"));

        public static Result IsAwsSqsOutboundV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.sqs.send")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsOptional("region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SQS")
                .Matches("aws_service", "SQS")
                .IsPresent("aws.queue.name")
                .IsPresent("queuename")
                .IsOptional("aws.queue.url")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "queuename", "peer.service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "producer"));

        public static Result IsAwsSqsRequestV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.sqs.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsOptional("region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SQS")
                .Matches("aws_service", "SQS")
                .IsPresent("aws.queue.name")
                .IsPresent("queuename")
                .IsOptional("aws.queue.url")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "queuename", "peer.service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "client"));

        public static Result IsAwsSnsInboundV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.sns.process")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsOptional("region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SNS")
                .Matches("aws_service", "SNS")
                .IsPresent("aws.topic.name")
                .IsPresent("topicname")
                .IsOptional("aws.topic.arn")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsOptional("_dd.base_service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "consumer"));

        public static Result IsAwsSnsOutboundV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.sns.send")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsOptional("region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SNS")
                .Matches("aws_service", "SNS")
                .IsPresent("aws.topic.name")
                .IsPresent("topicname")
                .IsOptional("aws.topic.arn")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "topicname", "peer.service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "producer"));

        public static Result IsAwsSnsRequestV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "aws.sns.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("aws.agent", "dotnet-aws-sdk")
                .IsPresent("aws.operation")
                .IsOptional("aws.region")
                .IsOptional("region")
                .IsPresent("aws.requestId")
                .Matches("aws.service", "SNS")
                .Matches("aws_service", "SNS")
                .IsPresent("aws.topic.name")
                .IsPresent("topicname")
                .IsOptional("aws.topic.arn")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "topicname", "peer.service")
                .Matches("component", "aws-sdk")
                .Matches("span.kind", "client"));

        public static Result IsAzureServiceBusInboundV1(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .MatchesOneOf(Name, "servicebus.receive", "servicebus.process", "consumer")
                .MatchesOneOf(Type, "http", "custom"))
            .Tags(s => s
                .Matches("az.namespace", "Microsoft.ServiceBus")
                .IsOptional("az.schema_url")
                .IfPresentMatchesOneOf("messaging.operation", "receive", "process")
                .IsOptional("messaging.source.name")
                .IsOptional("messaging.destination.name", "message_bus.destination")
                .IfPresentMatches("messaging.system", "servicebus")
                .IsOptional("net.peer.name")
                .IsOptional("peer.address")
                .IsOptional("server.address")
                .IsPresent("otel.library.name")
                .IsOptional("otel.library.version")
                .IsPresent("otel.trace_id")
                .MatchesOneOf("otel.status_code", "STATUS_CODE_UNSET", "STATUS_CODE_OK", "STATUS_CODE_ERROR")
                .IsOptional("otel.status_description")
                .IfPresentMatches("component", "servicebus")
                .IfPresentMatches("kind", "consumer")
                .Matches("span.kind", "consumer"));

        public static Result IsAzureServiceBusOutboundV1(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Name, "producer")
                .Matches(Type, "custom"))
            .Tags(s => s
                .Matches("az.namespace", "Microsoft.ServiceBus")
                .IsOptional("az.schema_url")
                .IsPresent("messaging.destination.name", "message_bus.destination")
                .IfPresentMatches("messaging.system", "servicebus")
                .IsOptional("net.peer.name")
                .IsOptional("peer.address")
                .IsOptional("server.address")
                .IsPresent("otel.library.name")
                .IsOptional("otel.library.version")
                .IsPresent("otel.trace_id")
                .MatchesOneOf("otel.status_code", "STATUS_CODE_UNSET", "STATUS_CODE_OK", "STATUS_CODE_ERROR")
                .IsOptional("otel.status_description")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "messaging.destination.name", "message_bus.destination", "peer.service")
                .IfPresentMatches("component", "servicebus")
                .IfPresentMatches("kind", "producer")
                .Matches("span.kind", "producer"));

        public static Result IsAzureServiceBusRequestV1(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .MatchesOneOf(Name, "servicebus.publish", "servicebus.settle", "client.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .Matches("az.namespace", "Microsoft.ServiceBus")
                .IsOptional("az.schema_url")
                .IsOptional("messaging.destination.name", "message_bus.destination")
                .IfPresentMatchesOneOf("messaging.operation", "publish", "settle")
                .IsOptional("messaging.source.name")
                .IfPresentMatches("messaging.system", "servicebus")
                .IsOptional("net.peer.name")
                .IsOptional("peer.address")
                .IsOptional("server.address")
                .IsPresent("otel.library.name")
                .IsOptional("otel.library.version")
                .IsPresent("otel.trace_id")
                .MatchesOneOf("otel.status_code", "STATUS_CODE_UNSET", "STATUS_CODE_OK", "STATUS_CODE_ERROR")
                .IsOptional("otel.status_description")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "messaging.destination.name", "message_bus.destination", "peer.service")
                .IfPresentMatches("component", "servicebus")
                .IfPresentMatches("kind", "client")
                .Matches("span.kind", "client"));

        public static Result IsCosmosDbV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "cosmosdb.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("cosmosdb.container")
                .IsOptional("db.name")
                .Matches("db.type", "cosmosdb")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "CosmosDb")
                .Matches("span.kind", "client"));

        public static Result IsCouchbaseV1(this MockSpan span) => Result.FromSpan(span)
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
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.couchbase.seed.nodes", "out.host", "peer.service")
                .Matches("component", "Couchbase")
                .Matches("span.kind", "client"));

        public static Result IsElasticsearchNetV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "elasticsearch.query")
                .Matches(Type, "elasticsearch"))
            .Tags(s => s
                .IsPresent("elasticsearch.action")
                .IsPresent("elasticsearch.method")
                .IsPresent("elasticsearch.url")
                .IsPresent("out.host")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "elasticsearch-net")
                .Matches("span.kind", "client"));

        public static Result IsGraphQLV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "graphql.execute", "graphql.validate")
                .Matches(Type, "graphql"))
            .Tags(s => s
                .IsOptional("graphql.operation.name")
                .IsOptional("graphql.operation.type")
                .IsPresent("graphql.source")
                .IsOptional("_dd.base_service")
                .Matches("component", "GraphQL")
                .Matches("span.kind", "server"));

        public static Result IsGrpcClientV1(this MockSpan span, ISet<string> excludeTags) => Result.FromSpan(span, excludeTags)
            .WithMarkdownSection("gRPC Client")
            .Properties(s => s
                .Matches(Name, "grpc.client.request")
                .Matches(Type, "grpc"))
            .Tags(s => s
                .IsPresent("grpc.method.kind")
                .IsPresent("grpc.method.name")
                .IsPresent("grpc.method.package")
                .IsPresent("grpc.method.path")
                .IsPresent("grpc.method.service")
                .IsPresent("grpc.status.code")
                .IsPresent("out.host")
                .IsPresent("peer.hostname")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "rpc.service", "out.host", "peer.service")
                .Matches("component", "Grpc")
                .Matches("span.kind", "client"));

        public static Result IsGrpcServerV1(this MockSpan span, ISet<string> excludeTags) => Result.FromSpan(span, excludeTags)
            .WithMarkdownSection("gRPC Server")
            .Properties(s => s
                .MatchesOneOf(Name, "grpc.server.request")
                .Matches(Type, "grpc"))
            .Tags(s => s
                .IsPresent("grpc.method.kind")
                .IsPresent("grpc.method.name")
                .IsPresent("grpc.method.package")
                .IsPresent("grpc.method.path")
                .IsPresent("grpc.method.service")
                .IsPresent("grpc.status.code")
                .IsOptional("_dd.base_service")
                .Matches("component", "Grpc")
                .Matches("span.kind", "server"));

        public static Result IsHotChocolateV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .MatchesOneOf(Name, "graphql.execute", "graphql.validate")
                .Matches(Type, "graphql"))
            .Tags(s => s
                .IsOptional("graphql.operation.name")
                .IsOptional("graphql.operation.type")
                .IsPresent("graphql.source")
                .IsOptional("_dd.base_service")
                .Matches("component", "HotChocolate")
                .Matches("span.kind", "server"));

        public static Result IsHttpMessageHandlerV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.client.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .IsPresent("http-client-handler-type")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("out.host")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .IsPresent("component")
                .Matches("span.kind", "client"));

        public static Result IsKafkaInboundV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Kafka - Inbound")
            .Properties(s => s
                .Matches(Name, "kafka.process")
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
                .IsOptional("_dd.base_service")
                .Matches("component", "kafka")
                .Matches("span.kind", "consumer"));

        public static Result IsKafkaOutboundV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Kafka - Outbound")
            .Properties(s => s
                .Matches(Name, "kafka.send")
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
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "messaging.kafka.bootstrap.servers", "peer.service")
                .Matches("component", "kafka")
                .Matches("span.kind", "producer"));

        public static Result IsMongoDbV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "mongodb.query")
                .Matches(Type, "mongodb"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsOptional("mongodb.collection")
                .IsOptional("mongodb.query")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "MongoDb")
                .Matches("span.kind", "client"));

        public static Result IsMsmqInboundV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Msmq - Inbound")
            .Properties(s => s
                .Matches(Name, "msmq.process")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("msmq.command")
                .IsOptional("msmq.message.transactional")
                .IsPresent("msmq.queue.path")
                .IsOptional("msmq.queue.transactional")
                .IsPresent("out.host")
                .IsOptional("_dd.base_service")
                .Matches("component", "msmq")
                .Matches("span.kind", "consumer"));

        public static Result IsMsmqOutboundV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Msmq - Outbound")
            .Properties(s => s
                .Matches(Name, "msmq.send")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("msmq.command")
                .IsOptional("msmq.message.transactional")
                .IsPresent("msmq.queue.path")
                .IsOptional("msmq.queue.transactional")
                .IsPresent("out.host")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "msmq")
                .Matches("span.kind", "producer"));

        public static Result IsMsmqClientV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Msmq - Client")
            .Properties(s => s
                .Matches(Name, "msmq.command")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("msmq.command")
                .IsOptional("msmq.message.transactional")
                .IsPresent("msmq.queue.path")
                .IsOptional("msmq.queue.transactional")
                .IsPresent("out.host")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "msmq")
                .Matches("span.kind", "client"));

        public static Result IsMySqlV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "mysql.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsPresent("db.name")
                .IsPresent("db.user")
                .IsPresent("out.host")
                .Matches("db.type", "mysql")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "MySql")
                .Matches("span.kind", "client")
                .IsOptional("_dd.dbm_trace_injected"));

        public static Result IsNpgsqlV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "postgres.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsPresent("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "postgres")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "Npgsql")
                .Matches("span.kind", "client")
                .IsOptional("_dd.dbm_trace_injected"));

        public static Result IsOpenTelemetryV1(this MockSpan span, ISet<string> resources, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => { })
            .AdditionalTags(s => s
                .PassesThroughSource("OTEL Resource Attributes", resources))
            .Tags(s => s
                // .IsOptional("events") // aka span events, added by the trace agent when the OTLP span is populated with events
                .IsPresent("otel.library.name")
                .IsOptional("otel.library.version")
                .IsPresent("otel.trace_id")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("otel.status_code", "STATUS_CODE_UNSET", "STATUS_CODE_OK", "STATUS_CODE_ERROR")
                .IsOptional("otel.status_description")
                .MatchesOneOf("span.kind", "internal", "server", "client", "producer", "consumer"));

        public static Result IsOracleV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "oracle.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsPresent("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "oracle")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "Oracle")
                .Matches("span.kind", "client"));

        public static Result IsProcessV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "command_execution")
                .Matches(Type, "system"))
            .Tags(s => s
                .IsOptional("cmd.environment_variables")
                .IsOptional("cmd.exec")
                .IsOptional("cmd.shell")
                .IsOptional("cmd.truncated")
                .IsOptional("_dd.base_service")
                .Matches("cmd.component", "process")
                .Matches("span.kind", "internal"));

        public static Result IsRabbitMQAdminV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Rabbit - Admin")
            .Properties(s => s
                .Matches(Name, "amqp.command")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("amqp.command")
                .IsPresent("out.host")
                .IsOptional("amqp.delivery_mode")
                .IsOptional("amqp.exchange")
                .IsOptional("amqp.routing_key")
                .IsOptional("amqp.queue")
                .IsOptional("message.size")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "RabbitMQ")
                .Matches("span.kind", "client"));

        public static Result IsRabbitMQInboundV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Rabbit - Inbound")
            .Properties(s => s
                .Matches(Name, "amqp.process")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("amqp.command")
                .IsOptional("amqp.delivery_mode")
                .IsOptional("amqp.exchange")
                .IsOptional("amqp.routing_key")
                .IsOptional("amqp.queue")
                .IsOptional("message.size")
                .IsOptional("_dd.base_service")
                .Matches("component", "RabbitMQ")
                .Matches("span.kind", "consumer"));

        public static Result IsRabbitMQOutboundV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Rabbit - Outbound")
            .Properties(s => s
                .Matches(Name, "amqp.send")
                .Matches(Type, "queue"))
            .Tags(s => s
                .IsPresent("amqp.command")
                .IsPresent("out.host")
                .IsOptional("amqp.delivery_mode")
                .IsOptional("amqp.exchange")
                .IsOptional("amqp.routing_key")
                .IsOptional("amqp.queue")
                .IsOptional("message.size")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "RabbitMQ")
                .Matches("span.kind", "producer"));

        public static Result IsRemotingClientV1(this MockSpan span) => Result.FromSpan(span)
           .Properties(s => s
                          .Matches(Name, "dotnet_remoting.client.request"))
           .Tags(s => s
                     .IsPresent("rpc.method")
                     .IsOptional("peer.service")
                     .IsOptional("peer.service.remapped_from")
                     .Matches("rpc.system", "dotnet_remoting")
                     .Matches("component", "Remoting")
                     .IfPresentMatchesOneOf("_dd.peer.service.source", "peer.service", "rpc.service")
                     .MatchesOneOf("span.kind", "client", "server"));

        public static Result IsRemotingServerV1(this MockSpan span) => Result.FromSpan(span)
             .Properties(s => s
                .Matches(Name, "dotnet_remoting.server.request"))
             .Tags(s => s
               .IsPresent("rpc.method")
               .Matches("rpc.system", "dotnet_remoting")
               .Matches("component", "Remoting")
               .MatchesOneOf("span.kind", "client", "server"));

        public static Result IsServiceRemotingClientV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Service Remoting - Client")
            .Properties(s => s
                .Matches(Name, "service_remoting.client"))
            .Tags(s => s
                .IsOptional("service-fabric.application-id")
                .IsOptional("service-fabric.application-name")
                .IsOptional("service-fabric.partition-id")
                .IsOptional("service-fabric.node-id")
                .IsOptional("service-fabric.node-name")
                .IsOptional("service-fabric.service-name")
                .IsPresent("service-fabric.service-remoting.uri")
                .IsOptional("service-fabric.service-remoting.service")
                .IsPresent("service-fabric.service-remoting.method-name")
                .IsOptional("service-fabric.service-remoting.method-id")
                .IsOptional("service-fabric.service-remoting.interface-id")
                .IsOptional("service-fabric.service-remoting.invocation-id")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "service-fabric.service-remoting.service", "service-fabric.service-remoting.uri", "peer.service")
                .Matches("span.kind", "client"));

        public static Result IsServiceRemotingServerV1(this MockSpan span) => Result.FromSpan(span)
            .WithMarkdownSection("Service Remoting - Server")
            .Properties(s => s
                .Matches(Name, "service_remoting.server"))
            .Tags(s => s
                .IsOptional("service-fabric.application-id")
                .IsOptional("service-fabric.application-name")
                .IsOptional("service-fabric.partition-id")
                .IsOptional("service-fabric.node-id")
                .IsOptional("service-fabric.node-name")
                .IsOptional("service-fabric.service-name")
                .IsPresent("service-fabric.service-remoting.uri")
                .IsOptional("service-fabric.service-remoting.service")
                .IsPresent("service-fabric.service-remoting.method-name")
                .IsOptional("service-fabric.service-remoting.method-id")
                .IsOptional("service-fabric.service-remoting.interface-id")
                .IsOptional("service-fabric.service-remoting.invocation-id")
                .IsOptional("_dd.base_service")
                .Matches("span.kind", "server"));

        public static Result IsServiceStackRedisV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "redis.command")
                .Matches(Type, "redis"))
            .Metrics(s => s
                .IsPresent("db.redis.database_index"))
            .Tags(s => s
                .IsPresent("redis.raw_command")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "ServiceStackRedis")
                .Matches("span.kind", "client"));

        public static Result IsStackExchangeRedisV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "redis.command")
                .Matches(Type, "redis"))
            .Metrics(s => s
                .IsOptional("db.redis.database_index"))
            .Tags(s => s
                .IsPresent("redis.raw_command")
                .IsPresent("out.host")
                .IsPresent("out.port")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .Matches("component", "StackExchangeRedis")
                .Matches("span.kind", "client"));

        public static Result IsSqliteV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "sqlite.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "sqlite")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .Matches("component", "Sqlite")
                .Matches("span.kind", "client"));

        public static Result IsSqlClientV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "sql-server.query")
                .Matches(Type, "sql"))
            .Tags(s => s
                .IsOptional("db.name")
                .IsPresent("out.host")
                .Matches("db.type", "sql-server")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "db.name", "out.host", "peer.service")
                .IsOptional("_dd.dbm_trace_injected")
                .IsOptional("dd.instrumentation.time_ms")
                .Matches("component", "SqlClient")
                .Matches("span.kind", "client"));

        public static Result IsWcfV1(this MockSpan span, ISet<string> excludeTags = null) => Result.FromSpan(span, excludeTags)
            .Properties(s => s
                .Matches(Name, "http.server.request")
                .Matches(Type, "web"))
            .Tags(s => s
                .IsOptional("http.method")
                .IsOptional("http.request.headers.host")
                .IsPresent("http.url")
                .IsOptional("_dd.base_service")
                .Matches("component", "Wcf")
                .Matches("span.kind", "server"));

        public static Result IsWebRequestV1(this MockSpan span) => Result.FromSpan(span)
            .Properties(s => s
                .Matches(Name, "http.client.request")
                .Matches(Type, "http"))
            .Tags(s => s
                .IsOptional("http-client-handler-type")
                .IsPresent("http.method")
                .IsPresent("http.status_code")
                .IsPresent("http.url")
                .IsPresent("out.host")
                .IsPresent("peer.service")
                .IsOptional("peer.service.remapped_from")
                .IsOptional("_dd.base_service")
                .MatchesOneOf("_dd.peer.service.source", "out.host", "peer.service")
                .MatchesOneOf("component", "HttpMessageHandler", "WebRequest")
                .Matches("span.kind", "client"));
    }
}
