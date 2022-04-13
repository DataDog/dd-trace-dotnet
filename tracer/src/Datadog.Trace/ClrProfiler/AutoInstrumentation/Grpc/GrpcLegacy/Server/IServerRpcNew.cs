// <copyright file="IServerRpcNew.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Server
{
    /// <summary>
    /// DuckType for ServerRpcNew
    /// Interface because used in constraints
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core/Internal/ServerRpcNew.cs
    /// </summary>
    internal interface IServerRpcNew
    {
        public IMetadata RequestMetadata { get; }
    }
}
