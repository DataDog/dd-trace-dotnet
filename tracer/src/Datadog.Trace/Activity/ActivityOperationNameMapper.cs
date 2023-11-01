// <copyright file="ActivityOperationNameMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
// TODO maybe rename to OpenTelemetryOperationNameMapper? or something better?
namespace Datadog.Trace.Activity
{
    /// <summary>
    /// Helper class to map <see cref="SpanKinds"/> and various tags on an Activity to a <see cref="Span.OperationName"/>.
    /// </summary>
    internal static class ActivityOperationNameMapper
    {
        private const string HttpRequestMethod = "http.request.method";
        private const string NetworkProtocolName = "network.protocol.name";
        private const string MessagingSystem = "messaging.system";
        private const string MessagingOperation = "messaging.operation";
        private const string RpcSystem = "rpc.system";
        private const string RpcService = "rpc.service";

        public static void MapToOperationName(Span span)
        {
            if (span is null)
            {
                return;
            }

            // TODO legacy operation name?

            if (!string.IsNullOrEmpty(span.OperationName))
            {
                return; // means that operation.name tag was present
            }

            string operationName = string.Empty;

            if (!span.TryGetTag(Tags.SpanKind, out var spanKind))
            {
                span.OperationName = "otel_unknown";
                return;
            }

            switch (spanKind)
            {
                // TODO basic implementation first to get tests passing
                case SpanKinds.Internal:
                    break;
                case SpanKinds.Server:
                    operationName = CreateOperationNameForServer(span);
                    break;
                case SpanKinds.Client:
                    operationName = CreateOperationNameForClient(span);
                    break;
                case SpanKinds.Producer:
                    operationName = CreateOperationNameForProducer(span);
                    break;
                case SpanKinds.Consumer:
                    operationName = CreateOperationNameForConsumer(span);
                    break;
                default:
                    operationName = "otel_unknown";
                    break;
            }

            if (string.IsNullOrEmpty(operationName))
            {
                operationName = spanKind;
            }

            // TODO I've copy pasted this from OTLPHelper just to not forget to do something with it if it makes sense
            // if (Tracer.Instance.Settings.OpenTelemetryLegacyOperationNameEnabled)
            // {
            //     span.OperationName = activity5.Source.Name switch
            //     {
            //         string libName when !string.IsNullOrEmpty(libName) => $"{libName}.{spanKind}",
            //         _ => $"opentelemetry.{spanKind}",
            //     };
            // }

            // TODO what if there is a tag from the activity "operation.name" do we honour that?
            span.OperationName = operationName.ToLower();
        }

        private static string CreateOperationNameForServer(Span span)
        {
            if (span.TryGetTag(HttpRequestMethod, out _))
            {
                return "http.server.request";
            }
            else if (span.TryGetTag(NetworkProtocolName, out var protocol))
            {
                return $"{protocol}.server.request";
            }
            else if (span.TryGetTag(RpcSystem, out var rpcSystem))
            {
                return $"{rpcSystem}.server.request";
            }
            else if (span.TryGetTag("graphql.operation.type", out var operationType))
            {
                return "graphql.server.request";
            }
            else if (span.TryGetTag("faas.trigger", out var trigger))
            {
                return $"{trigger}.invoke";
            }
            else if (span.TryGetTag(MessagingSystem, out var messagingSystem) &&
                     span.TryGetTag(MessagingOperation, out var messagingOperation))
            {
                return $"{messagingSystem}.{messagingOperation}";
            }

            return "server.request";
        }

        private static string CreateOperationNameForClient(Span span)
        {
            if (span.TryGetTag(HttpRequestMethod, out _))
            {
               return "http.client.request";
            }
            else if (span.TryGetTag("db.system", out var dbSystem))
            {
                return $"{dbSystem}.query";
            }
            else if (span.TryGetTag(MessagingSystem, out var messagingSystem) &&
                     span.TryGetTag(MessagingOperation, out var messagingOperation))
            {
                return $"{messagingSystem}.{messagingOperation}";
            }
            else if (span.TryGetTag(RpcSystem, out var rpcSystem))
            {
                _ = span.TryGetTag(RpcService, out var rpcService);
                if (rpcSystem == "aws-api")
                {
                    if (!string.IsNullOrEmpty(rpcService))
                    {
                        return $"aws.{rpcService.ToLower()}.request";
                    }

                    return "aws.client.request";
                }
                else
                {
                    return $"{rpcSystem}.client.request";
                }
            }
            else if (span.TryGetTag("faas.invoked_provider", out var provider) &&
                     span.TryGetTag("faas.name", out var faasName))
            {
                return $"{provider}.{faasName}.invoke";
            }
            else if (span.TryGetTag(NetworkProtocolName, out var protocol))
            {
                return $"{protocol}.client.request";
            }

            return "client.request";
        }

        private static string CreateOperationNameForProducer(Span span)
        {
            if (span.TryGetTag(MessagingSystem, out var messagingSystem) &&
                span.TryGetTag(MessagingOperation, out var messagingOperation))
            {
                return $"{messagingSystem}.{messagingOperation}";
            }

            return "producer";
        }

        private static string CreateOperationNameForConsumer(Span span)
        {
            if (span.TryGetTag(MessagingSystem, out var messagingSystem) &&
                span.TryGetTag(MessagingOperation, out var messagingOperation))
            {
                return $"{messagingSystem}.{messagingOperation}";
            }

            return "consumer";
        }

        // TODO hacky extension to just get tests passing
        private static bool TryGetTag(this Span span, string key, out string value)
        {
            value = span.Tags.GetTag(key);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return true;
        }
    }
}
