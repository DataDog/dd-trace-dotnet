// <copyright file="ActivityOperationNameMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// Helper class to map <see cref="SpanKinds"/> and various tags on an Activity to a <see cref="Span.OperationName"/>.
    /// </summary>
    internal static class ActivityOperationNameMapper
    {
        internal static void MapToOperationName(Span span)
        {
            if (!string.IsNullOrEmpty(span.OperationName))
            {
                return; // if OperationName has a value it means there was an "operation.name" tag
            }

            string operationName;

            if (span.Tags is not OpenTelemetryTags tags)
            {
                return; // TODO handle this if it is possible
            }

            if (tags.IsHttpServer())
            {
                operationName = "http.server.request";
            }
            else if (tags.IsHttpClient())
            {
                operationName = "http.client.request";
            }
            else if (tags.IsDatabase())
            {
                operationName = $"{tags.DbSystem}.query";
            }
            else if (tags.IsMessaging())
            {
                operationName = $"{tags.MessagingSystem}.{tags.MessagingOperation}";
            }
            else if (tags.IsAwsClient())
            {
                operationName = !string.IsNullOrEmpty(tags.RpcService) ? $"aws.{tags.RpcService}.request" : "aws.client.request";
            }
            else if (tags.IsRpcClient())
            {
                operationName = $"{tags.RpcSystem}.client.request";
            }
            else if (tags.IsRpcServer())
            {
                operationName = $"{tags.RpcSystem}.server.request";
            }
            else if (tags.IsFaasServer())
            {
                operationName = $"{tags.FaasTrigger}.invoke";
            }
            else if (tags.IsFaasClient())
            {
                operationName = $"{tags.FaasInvokedProvider}.{tags.FaasInvokedName}.invoke";
            }
            else if (tags.IsGraphQLServer())
            {
                operationName = "graphql.server.request";
            }
            else if (tags.IsGenericServer())
            {
                operationName = !string.IsNullOrEmpty(tags.NetworkProtocolName) ? $"{tags.NetworkProtocolName}.server.request" : "server.request";
            }
            else if (tags.IsGenericClient())
            {
                operationName = !string.IsNullOrEmpty(tags.NetworkProtocolName) ? $"{tags.NetworkProtocolName}.client.request" : "client.request";
            }
            else
            {
                operationName = !string.IsNullOrEmpty(tags.SpanKind) ? tags.SpanKind : "otel_unknown";
            }

            span.OperationName = operationName.ToLower();
        }

        private static bool IsHttpServer(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Server && !string.IsNullOrEmpty(tags.HttpRequestMethod);
        }

        private static bool IsHttpClient(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Client && !string.IsNullOrEmpty(tags.HttpRequestMethod);
        }

        private static bool IsDatabase(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Client && !string.IsNullOrEmpty(tags.DbSystem);
        }

        private static bool IsMessaging(this OpenTelemetryTags tags)
        {
            return (tags.SpanKind == SpanKinds.Client || tags.SpanKind == SpanKinds.Server || tags.SpanKind == SpanKinds.Producer || tags.SpanKind == SpanKinds.Consumer)
                && !string.IsNullOrEmpty(tags.MessagingSystem) && !string.IsNullOrEmpty(tags.MessagingOperation);
        }

        private static bool IsAwsClient(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Client && string.Equals(tags.RpcSystem, "aws-api", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRpcClient(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Client && !string.IsNullOrEmpty(tags.RpcSystem);
        }

        private static bool IsRpcServer(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Server && !string.IsNullOrEmpty(tags.RpcSystem);
        }

        private static bool IsFaasServer(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Server && !string.IsNullOrEmpty(tags.FaasTrigger);
        }

        private static bool IsFaasClient(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Client && !string.IsNullOrEmpty(tags.FaasInvokedProvider) && !string.IsNullOrEmpty(tags.FaasInvokedName);
        }

        private static bool IsGraphQLServer(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Server && !string.IsNullOrEmpty(tags.GraphQlOperationType);
        }

        private static bool IsGenericServer(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Server;
        }

        private static bool IsGenericClient(this OpenTelemetryTags tags)
        {
            return tags.SpanKind == SpanKinds.Client;
        }
    }
}
