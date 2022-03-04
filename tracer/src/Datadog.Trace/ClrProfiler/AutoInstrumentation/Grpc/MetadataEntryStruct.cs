// <copyright file="MetadataEntryStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    /// <summary>
    /// Duck type for Metadata.Entry
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core.Api/Metadata.cs#L276
    /// </summary>
    [DuckCopy]
    internal struct MetadataEntryStruct
    {
        public string Key;
        public bool IsBinary;
        public string? Value;
    }
}
