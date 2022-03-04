// <copyright file="ServerCallHandlerBaseStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer
{
    /// <summary>
    /// Duck type for Grpc.AspNetCore.Server.Internal.CallHandlers.ServerCallHandlerBase{Service, TRequest, TResponse}
    /// </summary>
    [DuckCopy]
    internal struct ServerCallHandlerBaseStruct
    {
        public ServerMethodInvokerBaseStruct MethodInvoker;
    }
}
