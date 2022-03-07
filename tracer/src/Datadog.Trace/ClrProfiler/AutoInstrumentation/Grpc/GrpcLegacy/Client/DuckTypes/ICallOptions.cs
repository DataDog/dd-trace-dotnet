// <copyright file="ICallOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes
{
    /// <summary>
    /// Duck type for CallOptions
    /// Interface as need to call the WithHeaders method
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core.Api/CallOptions.cs
    /// </summary>
    internal interface ICallOptions
    {
        public IMetadata? Headers { get; }

        public object WithHeaders(object headers);
    }
}
