// <copyright file="IClientSideStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes
{
    /// <summary>
    /// Duck type for Grpc.Core.Internal.ClientSideStatus
    /// Interface for use in constraints
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core/Internal/ClientSideStatus.cs
    /// </summary>
    internal interface IClientSideStatus
    {
        public StatusStruct Status { get; }

        public IMetadata? Trailers { get; }
    }
}
