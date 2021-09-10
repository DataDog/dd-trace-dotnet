// <copyright file="IntegrationIds.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    internal enum IntegrationIds
    {
        HttpMessageHandler,
        HttpSocketsHandler,
        WinHttpHandler,
        CurlHandler,
        AspNetCore,
        AdoNet,
        AspNet,
        AspNetMvc,
        AspNetWebApi2,
        GraphQL,
        MongoDb,
        XUnit,
        NUnit,
        MsTestV2,
        Wcf,
        WebRequest,
        ElasticsearchNet5,
        ElasticsearchNet, // NOTE: keep this name without the 6 to avoid breaking changes
        ServiceStackRedis,
        StackExchangeRedis,
        ServiceRemoting,
        RabbitMQ,
        Msmq,
        Kafka,
        CosmosDb,
        AwsSdk,
        AwsSqs,
        ILogger,
        Aerospike,
    }
}
