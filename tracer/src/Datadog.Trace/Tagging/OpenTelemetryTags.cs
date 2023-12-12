// <copyright file="OpenTelemetryTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class OpenTelemetryTags : CommonTags
    {
        [Tag(Tags.SpanKind)]
        public virtual string SpanKind { get; set; }

        [Tag("http.request.method")]
        public string HttpRequestMethod { get; set; }

        [Tag("db.system")]
        public string DbSystem { get; set; }

        [Tag("messaging.system")]
        public string MessagingSystem { get; set; }

        [Tag(Trace.Tags.MessagingOperation)]
        public string MessagingOperation { get; set; }

        [Tag("rpc.system")]
        public string RpcSystem { get; set; }

        [Tag("rpc.service")]
        public string RpcService { get; set; }

        [Tag("faas.invoked_provider")]
        public string FaasInvokedProvider { get; set; }

        [Tag("faas.invoked_name")]
        public string FaasInvokedName { get; set; }

        [Tag("faas.trigger")]
        public string FaasTrigger { get; set; }

        [Tag("graphql.operation.type")]
        public string GraphQlOperationType { get; set; }

        [Tag("network.protocol.name")]
        public string NetworkProtocolName { get; set; }
    }
}
