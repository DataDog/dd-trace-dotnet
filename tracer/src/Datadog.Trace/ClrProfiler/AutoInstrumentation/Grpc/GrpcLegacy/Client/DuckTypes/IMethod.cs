// <copyright file="IMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes
{
    /// <summary>
    /// Duck type for Method{TRequest, TResponse}
    /// Interface for use in constraints
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core.Api/Method.cs
    /// </summary>
    internal interface IMethod
    {
        public string? ServiceName { get; }

        public string? Name { get; }

        public string? FullName { get; }

        [Duck(Name = "Type")]
        public int GrpcType { get; }
    }
}
