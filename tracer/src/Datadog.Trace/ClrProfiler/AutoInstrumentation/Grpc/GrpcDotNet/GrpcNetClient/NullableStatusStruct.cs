// <copyright file="NullableStatusStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NET461

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient
{
    /// <summary>
    /// Duck type for Nullable{IStatus} - required to use constraints
    /// </summary>
    internal struct NullableStatusStruct
    {
        public StatusStruct Value;
    }
}
#endif
