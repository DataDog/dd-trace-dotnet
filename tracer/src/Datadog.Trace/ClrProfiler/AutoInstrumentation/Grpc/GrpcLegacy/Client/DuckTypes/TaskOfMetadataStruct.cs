// <copyright file="TaskOfMetadataStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes
{
    /// <summary>
    /// Duck type Task{Metadata}
    /// </summary>
    [DuckCopy]
    internal struct TaskOfMetadataStruct
    {
        public bool IsCompleted;
        public IMetadata? Result;
    }
}
