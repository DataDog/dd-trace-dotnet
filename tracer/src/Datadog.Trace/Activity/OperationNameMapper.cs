// <copyright file="OperationNameMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// Helper class to map <see cref="SpanKinds"/> and various tags on an <c>Activity</c> or OpenTelemetry Span to a <see cref="Span.OperationName"/>.
    /// </summary>
    internal static class OperationNameMapper
    {
        [TestingOnly]
        internal static void MapToOperationName(Span span)
        {
            if (!string.IsNullOrEmpty(span.OperationName))
            {
                return; // if OperationName has a value it means there was an "operation.name" tag
            }

            if (span.Tags is not OpenTelemetryTags tags)
            {
                // while span.Tags can be something other than OpenTelemetryTags the
                // ActivityHandlerCommon.StartActivity requires its "Tags" type to be
                // OpenTelemetryTags to ensure that for all Activity objects that we
                // process will be able to be mapped correctly.
                return;
            }

            span.OperationName = GetOperationName(tags);
        }

        internal static string GetOperationName(OpenTelemetryTags tags)
        {
            var httpRequestMethod = tags.GetTag("http.request.method");
            if (tags.SpanKind == SpanKinds.Server && !string.IsNullOrEmpty(httpRequestMethod))
            {
                // IsHttpServer
                return "http.server.request";
            }

            if (tags.SpanKind == SpanKinds.Client && !string.IsNullOrEmpty(httpRequestMethod))
            {
                // IsHttpClient
                return "http.client.request";
            }

            var dbSystem = tags.GetTag("db.system");
            if (tags.SpanKind == SpanKinds.Client && !StringUtil.IsNullOrEmpty(dbSystem))
            {
                // IsDatabase
                return $"{dbSystem.ToLowerInvariant()}.query";
            }

            var messagingSystem = tags.GetTag(Tags.MessagingSystem);
            var messagingOperation = tags.GetTag(Tags.MessagingOperation);
            if (tags.SpanKind is SpanKinds.Client or SpanKinds.Server or SpanKinds.Producer or SpanKinds.Consumer
                  && !StringUtil.IsNullOrEmpty(messagingSystem) &&
                     !StringUtil.IsNullOrEmpty(messagingOperation))
            {
                // IsMessaging
                return $"{messagingSystem}.{messagingOperation}".ToLowerInvariant();
            }

            var rpcSystem = tags.GetTag(Tags.RpcSystem);
            if (tags.SpanKind == SpanKinds.Client && string.Equals(rpcSystem, "aws-api", StringComparison.OrdinalIgnoreCase))
            {
                // IsAwsClient
                var service = tags.GetTag(Tags.RpcService)?.ToLowerInvariant();
                return !StringUtil.IsNullOrEmpty(service) ? $"aws.{service}.request" : "aws.client.request";
            }

            if (tags.SpanKind == SpanKinds.Client && !StringUtil.IsNullOrEmpty(rpcSystem))
            {
                // IsRpcClient
                return $"{rpcSystem.ToLowerInvariant()}.client.request";
            }

            if (tags.SpanKind == SpanKinds.Server && !StringUtil.IsNullOrEmpty(rpcSystem))
            {
                // IsRpcServer
                return $"{rpcSystem.ToLowerInvariant()}.server.request";
            }

            var faasTrigger = tags.GetTag("faas.trigger");
            if (tags.SpanKind == SpanKinds.Server && !StringUtil.IsNullOrEmpty(faasTrigger))
            {
                // IsFaasServer
                return $"{faasTrigger.ToLowerInvariant()}.invoke";
            }

            var faasInvokedProvider = tags.GetTag("faas.invoked_provider");
            var faasInvokedName = tags.GetTag("faas.invoked_name");
            if (tags.SpanKind == SpanKinds.Client && !StringUtil.IsNullOrEmpty(faasInvokedProvider) && !StringUtil.IsNullOrEmpty(faasInvokedName))
            {
                // IsFaasClient
                return $"{faasInvokedProvider}.{faasInvokedName}.invoke".ToLowerInvariant();
            }

            if (tags.SpanKind == SpanKinds.Server && !StringUtil.IsNullOrEmpty(tags.GetTag("graphql.operation.type")))
            {
                // IsGraphQLServer
                return "graphql.server.request";
            }

            if (tags.SpanKind == SpanKinds.Server)
            {
                // IsGenericServer
                var name = tags.GetTag("network.protocol.name");
                return !StringUtil.IsNullOrEmpty(name) ? $"{name.ToLowerInvariant()}.server.request" : "server.request";
            }

            if (tags.SpanKind == SpanKinds.Client)
            {
                // IsGenericClient
                var name = tags.GetTag("network.protocol.name");
                return !StringUtil.IsNullOrEmpty(name) ? $"{name.ToLowerInvariant()}.client.request" : "client.request";
            }

            // when there is no SpanKind defined (possible on Activity objects without "Kind")
            // fallback to using "internal" for the name.
            return !StringUtil.IsNullOrEmpty(tags.SpanKind) ? tags.SpanKind.ToLowerInvariant() : SpanKinds.Internal;
        }
    }
}
