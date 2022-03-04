// <copyright file="MethodStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    /// <summary>
    /// Duck type for Grpc.Core.Method{TRequest, TResponse}
    /// </summary>
    [DuckCopy]
    internal struct MethodStruct
    {
        public string? ServiceName;
        public string? Name;
        public string? FullName;

        [Duck(Name = "Type")]
        public int GrpcType;
    }
}
