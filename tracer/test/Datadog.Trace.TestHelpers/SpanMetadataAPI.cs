// <copyright file="SpanMetadataAPI.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    public static class SpanMetadataAPI
    {
        public static Result IsAdoNet(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAdoNetV1(),
                _ => span.IsAdoNetV0(),
            };

        public static Result IsAerospike(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAerospikeV1(),
                _ => span.IsAerospikeV0(),
            };

        public static Result IsAspNet(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAspNetV1(),
                _ => span.IsAspNetV0(),
            };

        public static Result IsAspNetMvc(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAspNetMvcV1(),
                _ => span.IsAspNetMvcV0(),
            };

        public static Result IsAspNetWebApi2(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAspNetWebApi2V1(),
                _ => span.IsAspNetWebApi2V0(),
            };

        public static Result IsAspNetCore(this MockSpan span, string metadataSchemaVersion, ISet<string> excludeTags = null) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAspNetCoreV1(excludeTags),
                _ => span.IsAspNetCoreV0(excludeTags),
            };

        public static Result IsAspNetCoreMvc(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAspNetCoreMvcV1(),
                _ => span.IsAspNetCoreMvcV0(),
            };

        public static Result IsAwsSqsInbound(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAwsSqsInboundV1(),
                _ => span.IsAwsSqsRequestV0(),
            };

        public static Result IsAwsSqsOutbound(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAwsSqsOutboundV1(),
                _ => span.IsAwsSqsRequestV0(),
            };

        public static Result IsAwsSqsRequest(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAwsSqsRequestV1(),
                _ => span.IsAwsSqsRequestV0(),
            };

        public static Result IsAwsSnsInbound(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAwsSnsInboundV1(),
                _ => span.IsAwsSnsRequestV0(),
            };

        public static Result IsAwsSnsOutbound(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAwsSnsOutboundV1(),
                _ => span.IsAwsSnsRequestV0(),
            };

        public static Result IsAwsSnsRequest(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsAwsSnsRequestV1(),
                _ => span.IsAwsSnsRequestV0(),
            };

        public static Result IsCosmosDb(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsCosmosDbV1(),
                _ => span.IsCosmosDbV0(),
            };

        public static Result IsCouchbase(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsCouchbaseV1(),
                _ => span.IsCouchbaseV0(),
            };

        public static Result IsElasticsearchNet(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsElasticsearchNetV1(),
                _ => span.IsElasticsearchNetV0(),
            };

        public static Result IsGraphQL(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsGraphQLV1(),
                _ => span.IsGraphQLV0(),
            };

        public static Result IsGrpcClient(this MockSpan span, string metadataSchemaVersion, ISet<string> excludeTags) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsGrpcClientV1(excludeTags),
                _ => span.IsGrpcClientV0(excludeTags),
            };

        public static Result IsGrpcServer(this MockSpan span, string metadataSchemaVersion, ISet<string> excludeTags) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsGrpcServerV1(excludeTags),
                _ => span.IsGrpcServerV0(excludeTags),
            };

        public static Result IsHotChocolate(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsHotChocolateV1(),
                _ => span.IsHotChocolateV0(),
            };

        public static Result IsHttpMessageHandler(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsHttpMessageHandlerV1(),
                _ => span.IsHttpMessageHandlerV0(),
            };

        public static Result IsKafkaInbound(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsKafkaInboundV1(),
                _ => span.IsKafkaInboundV0(),
            };

        public static Result IsKafkaOutbound(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsKafkaOutboundV1(),
                _ => span.IsKafkaOutboundV0(),
            };

        public static Result IsMongoDb(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsMongoDbV1(),
                _ => span.IsMongoDbV0(),
            };

        public static Result IsMsmq(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsMsmqV1(),
                _ => span.IsMsmqV0(),
            };

        public static Result IsMySql(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsMySqlV1(),
                _ => span.IsMySqlV0(),
            };

        public static Result IsNpgsql(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsNpgsqlV1(),
                _ => span.IsNpgsqlV0(),
            };

        public static Result IsOpenTelemetry(this MockSpan span, string metadataSchemaVersion, ISet<string> resources, ISet<string> excludeTags) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsOpenTelemetryV1(resources, excludeTags),
                _ => span.IsOpenTelemetryV0(resources, excludeTags),
            };

        public static Result IsOracle(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsOracleV1(),
                _ => span.IsOracleV0(),
            };

        public static Result IsProcess(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsProcessV1(),
                _ => span.IsProcessV0(),
            };

        public static Result IsRabbitMQ(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsRabbitMQV1(),
                _ => span.IsRabbitMQV0(),
            };

        public static Result IsServiceRemoting(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsServiceRemotingV1(),
                _ => span.IsServiceRemotingV0(),
            };

        public static Result IsServiceStackRedis(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsServiceStackRedisV1(),
                _ => span.IsServiceStackRedisV0(),
            };

        public static Result IsStackExchangeRedis(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsStackExchangeRedisV1(),
                _ => span.IsStackExchangeRedisV0(),
            };

        public static Result IsSqlite(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsSqliteV1(),
                _ => span.IsSqliteV0(),
            };

        public static Result IsSqlClient(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsSqlClientV1(),
                _ => span.IsSqlClientV0(),
            };

        public static Result IsWcf(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsWcfV1(),
                _ => span.IsWcfV0(),
            };

        public static Result IsWebRequest(this MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsWebRequestV1(),
                _ => span.IsWebRequestV0(),
            };
    }
}
