// <copyright file="HttpContextServerCallContextStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer
{
    /// <summary>
    /// Duck type for HttpContextServerCallContext
    /// Interface as used in constraints
    /// </summary>
    internal struct HttpContextServerCallContextStruct
    {
        public StatusStruct StatusCore;

        public IMetadata? ResponseTrailers;

        public IMetadata? RequestHeaders;
    }
}
