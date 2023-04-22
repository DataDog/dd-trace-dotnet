// <copyright file="MessagingSchemaV1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration.Schema.V1
{
    internal class MessagingSchemaV1 : IMessagingSchema
    {
        public string GetInboundOperationName(string messagingSystem) => $"{messagingSystem}.process";

        public string GetInboundServiceName(string defaultServiceName, string messagingSystem) => defaultServiceName;

        public string GetOutboundOperationName(string messagingSystem) => $"{messagingSystem}.send";

        public string GetOutboundServiceName(string defaultServiceName, string databaseType) => defaultServiceName;
    }
}
