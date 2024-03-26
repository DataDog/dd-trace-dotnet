// <copyright file="GrpcCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    internal static class GrpcCommon
    {
        public const string RequestMetadataTagPrefix = "grpc.request.metadata";
        public const string ResponseMetadataTagPrefix = "grpc.response.metadata";

        public static void AddGrpcTags(GrpcTags tags, Tracer tracer, int grpcType, string? name, string? path, string? serviceName, bool analyticsEnabledWithGlobalSetting = false)
            => AddGrpcTags(tags, tracer, GetGrpcMethodKind(grpcType), name, path, serviceName, analyticsEnabledWithGlobalSetting);

        public static void AddGrpcTags(GrpcTags tags, Tracer tracer, string grpcType, string? name, string? path, string? serviceName, bool analyticsEnabledWithGlobalSetting = false)
        {
            tags.MethodKind = grpcType;
            tags.MethodName = name;
            tags.MethodPath = path;

            if (!string.IsNullOrEmpty(serviceName))
            {
                // get the package and service name
                var indexOf = serviceName!.LastIndexOf('.');

                if (indexOf > 0 && indexOf < serviceName.Length - 2)
                {
                    tags.MethodPackage = serviceName.Substring(startIndex: 0, length: indexOf);
                    tags.MethodService = serviceName.Substring(startIndex: indexOf + 1);
                }
                else
                {
                    tags.MethodService = serviceName;
                }
            }

            tags.SetAnalyticsSampleRate(IntegrationId.Grpc, tracer.Settings, analyticsEnabledWithGlobalSetting);
        }

        public static string GetGrpcMethodKind(int value)
            => value switch
            {
                // Values from Grpc.Core.MethodType
                0 => GrpcMethodKinds.Unary,
                1 => GrpcMethodKinds.ClientStreaming,
                2 => GrpcMethodKinds.ServerStreaming,
                3 => GrpcMethodKinds.DuplexStreaming,
                _ => "Unknown", // No other values are valid, but don't want to throw here
            };

        public static void RecordFinalClientSpanStatus(Tracer tracer, int grpcStatusCode, string errorMessage, Exception? ex)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc)
             || tracer.ActiveScope?.Span is not Span { Tags: GrpcClientTags } span)
            {
                return;
            }

            RecordFinalStatus(span, grpcStatusCode, errorMessage, ex);
        }

        public static void RecordFinalStatus(Span span, int grpcStatusCode, string errorMessage, Exception? ex)
        {
            var tags = (GrpcTags)span.Tags;
            tags.StatusCode = grpcStatusCode.ToString();

            if (grpcStatusCode != 0)
            {
                if (ex is null)
                {
                    span.Error = true;
                    // Message can be null if the user throws an RpcException without a message
                    // e.g. new RpcException(Cancelled).
                    span.SetTag(Tags.ErrorMsg, string.IsNullOrEmpty(errorMessage) ? GetErrorDescription(grpcStatusCode) : errorMessage);
                }
                else
                {
                    span.SetException(ex);
                }
            }
        }

        /// <summary>
        /// Return a description of the grpcStatus Code
        /// See https://pkg.go.dev/google.golang.org/grpc/codes for values
        /// </summary>
        private static string GetErrorDescription(int grpcStatusCode)
            => grpcStatusCode switch
            {
                0 => "OK",
                1 => "Canceled",
                2 => "Unknown",
                3 => "InvalidArgument",
                4 => "DeadlineExceeded",
                5 => "NotFound",
                6 => "AlreadyExists",
                7 => "PermissionDenied",
                8 => "ResourceExhausted",
                9 => "FailedPrecondition",
                10 => "Aborted",
                11 => "OutOfRange",
                12 => "Unimplemented",
                13 => "Internal",
                14 => "Unavailable",
                15 => "DataLoss",
                16 => "Unauthenticated",
                _ => "Unknown"
            };
    }
}
