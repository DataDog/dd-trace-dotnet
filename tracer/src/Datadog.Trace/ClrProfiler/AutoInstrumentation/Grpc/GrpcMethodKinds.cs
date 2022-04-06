// <copyright file="GrpcMethodKinds.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    internal class GrpcMethodKinds
    {
        public const string Unary = "unary";
        public const string ClientStreaming = "client_streaming";
        public const string ServerStreaming = "server_streaming";
        public const string DuplexStreaming = "bidi_streaming";
    }
}
