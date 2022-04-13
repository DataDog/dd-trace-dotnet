// <copyright file="ServerCallHandlerStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Server
{
    /// <summary>
    /// Duck type for all implementations of IServerCallHandler
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core/Internal/ServerCallHandler.cs
    /// </summary>
    [DuckCopy]
    internal struct ServerCallHandlerStruct
    {
        [DuckField(Name = "method")]
        public MethodStruct Method;
    }
}
