// <copyright file="IMessagingSchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration.Schema
{
    internal interface IMessagingSchema
    {
        string GetInboundOperationName(string messagingSystem);

        string GetInboundServiceName(string applicationName, string messagingSystem);

        string GetOutboundOperationName(string messagingSystem);

        string GetOutboundServiceName(string applicationName, string databaseType);
    }
}
