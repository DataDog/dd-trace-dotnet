// <copyright file="ActivityOperationNameMapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// Helper class to map <see cref="SpanKinds"/> and various tags on an Activity to a <see cref="Span.OperationName"/>.
    /// </summary>
    internal static class ActivityOperationNameMapper
    {
        private const string CodeNamespace = "code.namespace";
        private const string CodeFunction = "code.function";
        private const string HttpRequestMethod = "http.request.method";
        private const string NetworkProtocolName = "network.protocol.name";
        private const string MessagingSystem = "messaging.system";
        private const string MessagingOperation = "messaging.operation";
        private const string RpcSystem = "rpc.system";
        private const string RpcService = "rpc.service";

        public static void MapToOperationName(Span span)
        {
            string operationName = string.Empty;
            var spanKind = span.GetTag(Tags.SpanKind);
            switch (spanKind)
            {
                // TODO basic implementation first to get tests passing
                case SpanKinds.Internal:
                    if (span.TryGetTag(CodeNamespace, out var codeNamespace) &&
                        span.TryGetTag(CodeFunction, out var codeFunction))
                    {
                        operationName = $"{codeNamespace}.{codeFunction}";
                    }

                    break;
                case SpanKinds.Server:
                    if (span.TryGetTag(HttpRequestMethod, out _))
                    {
                        operationName = "http.server.request";
                        break;
                    }
                    else if (span.TryGetTag(NetworkProtocolName, out var protocol))
                    {
                        operationName = $"{protocol}.server.request";
                        break;
                    }
                    else if (span.TryGetTag(RpcSystem, out var rpcSystem))
                    {
                        // TODO do we care about rpcSErvice
                        operationName = $"{rpcSystem}.server.request";
                        break;
                    }
                    else if (span.TryGetTag("graphql.operation.type", out var operationType))
                    {
                        operationName = $"graphql.{operationType}";
                        break;
                    }
                    else if (span.TryGetTag("faas.trigger", out var trigger))
                    {
                        operationName = $"{trigger}.trigger";
                        break;
                    }

                    operationName = "unknown.server.request";
                    break;
                case SpanKinds.Client:
                    if (span.TryGetTag(HttpRequestMethod, out _))
                    {
                        operationName = "http.client.request";
                        break;
                    }
                    else if (span.TryGetTag("db.system", out var dbSystem))
                    {
                        if (span.TryGetTag("db.operation", out var dbOperation))
                        {
                            operationName = $"{dbSystem}.{dbOperation}";
                            break;
                        }

                        operationName = $"{dbSystem}.query";
                        break;
                    }
                    else if (span.TryGetTag(MessagingSystem, out var messagingSystem) &&
                             span.TryGetTag(MessagingOperation, out var messagingOperation))
                    {
                        operationName = $"{messagingSystem}.{messagingOperation}";
                        break;
                    }
                    else if (span.TryGetTag(RpcSystem, out var rpcSystem))
                    {
                        _ = span.TryGetTag(RpcService, out var rpcService);
                        if (rpcSystem == "aws-api")
                        {
                            if (!string.IsNullOrEmpty(rpcService))
                            {
                                operationName = $"aws.{rpcService.ToLower()}";
                                break;
                            }

                            operationName = "aws";
                            break;
                        }
                        else
                        {
                            // TODO do we care about the rpc.service?
                            operationName = $"{rpcSystem}.client.request";
                            break;
                        }
                    }
                    else if (span.TryGetTag("faas.invoked_provider", out var provider))
                    {
                        operationName = $"{provider}.invoke";
                        break;
                    }
                    else if (span.TryGetTag(NetworkProtocolName, out var protocol))
                    {
                        operationName = $"{protocol}.client.request";
                        break;
                    }

                    operationName = "unknown.client.request"; // TODO fallback value no clue what to do here
                    break;
                case SpanKinds.Producer:
                    if (span.TryGetTag(MessagingSystem, out var messagingSystem2) &&
                             span.TryGetTag(MessagingOperation, out var messagingOperation2))
                    {
                        operationName = $"{messagingSystem2}.{messagingOperation2}";
                        break;
                    }

                    operationName = "unknown.producer.request"; // TODO fallback value no clue what to do here
                    break;
                case SpanKinds.Consumer:
                    if (span.TryGetTag(MessagingSystem, out var messagingSystem3) &&
                         span.TryGetTag(MessagingOperation, out var messagingOperation3))
                    {
                        operationName = $"{messagingSystem3}.{messagingOperation3}";
                        break;
                    }

                    operationName = "unknown.consumer.request"; // TODO fallback value no clue what to do here
                    break;
                default:
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
            span.OperationName = operationName;
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
