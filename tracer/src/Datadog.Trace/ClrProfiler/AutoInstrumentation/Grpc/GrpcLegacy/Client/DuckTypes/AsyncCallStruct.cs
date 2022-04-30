// <copyright file="AsyncCallStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes
{
    /// <summary>
    /// Duck Type for AsyncCall
    /// https://github.com/grpc/grpc/blob/master/src/csharp/Grpc.Core/Internal/AsyncCall.cs
    /// </summary>
    [DuckCopy]
    internal struct AsyncCallStruct
    {
        public CallInvocationDetailsStruct Details;

        [DuckField(Name = "finishedStatus")]
        public NullableClientSideStatusStruct FinishedStatus;

        /// <summary>
        /// This is a Task{Metadata} but can't use duck chaining here, otherwise
        /// we end up deadlocking trying to copy
        /// </summary>
        public Task ResponseHeadersAsync;
    }
}
