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
                    operationName = CreateOperationNameForInternal(span);
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

        private static string CreateOperationNameForInternal(Span span)
        {
            if (span.TryGetTag(CodeNamespace, out var codeNamespace) &&
                span.TryGetTag(CodeFunction, out var codeFunction))
            {
                return $"{codeNamespace}.{codeFunction}";
            }

            return string.Empty;
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
                // TODO do we care about rpcService
                return $"{rpcSystem}.server.request";
            }
            else if (span.TryGetTag("graphql.operation.type", out var operationType))
            {
                return $"graphql.{operationType}";
            }
            else if (span.TryGetTag("faas.trigger", out var trigger))
            {
                return $"{trigger}.trigger";
            }

            return "unknown.server.request";
        }

        private static string CreateOperationNameForClient(Span span)
        {
            if (span.TryGetTag(HttpRequestMethod, out _))
            {
               return "http.client.request";
            }
            else if (span.TryGetTag("db.system", out var dbSystem))
            {
                if (span.TryGetTag("db.operation", out var dbOperation))
                {
                    return $"{dbSystem}.{dbOperation}";
                }

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
                        return $"aws.{rpcService.ToLower()}";
                    }

                    return "aws";
                }
                else
                {
                    // TODO do we care about the rpc.service?
                    return $"{rpcSystem}.client.request";
                }
            }
            else if (span.TryGetTag("faas.invoked_provider", out var provider))
            {
                return $"{provider}.invoke";
            }
            else if (span.TryGetTag(NetworkProtocolName, out var protocol))
            {
                return $"{protocol}.client.request";
            }

            return "unknown.client.request"; // TODO fallback value no clue what to do here
        }

        private static string CreateOperationNameForProducer(Span span)
        {
            if (span.TryGetTag(MessagingSystem, out var messagingSystem2) &&
                             span.TryGetTag(MessagingOperation, out var messagingOperation2))
            {
                return $"{messagingSystem2}.{messagingOperation2}";
            }

            return "unknown.producer.request"; // TODO fallback value no clue what to do here
        }

        private static string CreateOperationNameForConsumer(Span span)
        {
            if (span.TryGetTag(MessagingSystem, out var messagingSystem3) &&
                         span.TryGetTag(MessagingOperation, out var messagingOperation3))
            {
                return $"{messagingSystem3}.{messagingOperation3}";
            }

            return "unknown.consumer.request"; // TODO fallback value no clue what to do here
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
