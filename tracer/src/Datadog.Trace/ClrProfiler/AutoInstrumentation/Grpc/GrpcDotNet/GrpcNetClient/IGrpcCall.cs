// <copyright file="IGrpcCall.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NET461

using System.Net.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient
{
    /// <summary>
    /// Duck type for Grpc.Net.Client.Internal.GrpcCall{TRequest, TResponse}
    /// </summary>
    internal interface IGrpcCall
    {
        public IChannel Channel { get; }

        public MethodStruct Method { get; }

        public CallOptionsStruct Options { get; }

        public HttpResponseMessage? HttpResponse { get; }

        public bool TryGetTrailers(out IMetadata metadata);
    }
}
#endif
