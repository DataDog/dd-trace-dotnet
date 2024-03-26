// <copyright file="CallInvocationDetailsStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes
{
    /// <summary>
    /// Duck type for CallInvocationDetails
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core/CallInvocationDetails.cs
    /// </summary>
    [DuckCopy]
    internal struct CallInvocationDetailsStruct
    {
        public IChannel Channel;
        public string Method;
        public CallOptionsStruct Options;
    }
}
