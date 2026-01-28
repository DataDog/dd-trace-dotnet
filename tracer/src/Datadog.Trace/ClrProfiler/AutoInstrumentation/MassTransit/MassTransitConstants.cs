// <copyright file="MassTransitConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal static class MassTransitConstants
    {
        internal const string IntegrationName = nameof(IntegrationId.MassTransit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.MassTransit;

        internal const string MessagingType = "masstransit";

        // Operation types for resource naming (matching MT8 OTEL patterns)
        internal const string OperationPublish = "publish";
        internal const string OperationSend = "send";
        internal const string OperationReceive = "receive";
        internal const string OperationProcess = "process";

        // MT8 OTEL-style span names - operation name is based on messaging system
        // Examples: "in_memory.send", "rabbitmq.send", "azureservicebus.send"
        // Consumer spans use "consumer" as the operation name
        internal const string ConsumerOperationName = "consumer";

        // Assembly and type names
        internal const string MassTransitAssembly = "MassTransit";
        internal const string ISendEndpointTypeName = "MassTransit.ISendEndpoint";
        internal const string IConsumeContextTypeName = "MassTransit.ConsumeContext`1";
        internal const string IConsumerTypeName = "MassTransit.IConsumer`1";
    }
}
