// <copyright file="MessagingSchemaV0.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration.Schema.V0
{
    internal class MessagingSchemaV0 : IMessagingSchema
    {
        public string GetInboundOperationName(string messagingSystem) => $"{messagingSystem}.consume";

        public string GetInboundServiceName(string defaultServiceName, string messagingSystem) => $"{defaultServiceName}-{messagingSystem}";

        public string GetOutboundOperationName(string messagingSystem) => $"{messagingSystem}.produce";

        public string GetOutboundServiceName(string defaultServiceName, string messagingSystem) => $"{defaultServiceName}-{messagingSystem}";
    }
}
