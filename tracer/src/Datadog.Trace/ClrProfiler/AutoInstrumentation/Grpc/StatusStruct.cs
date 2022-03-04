// <copyright file="StatusStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    /// <summary>
    /// Duck type for Grpc.Core.Status
    /// </summary>
    [DuckCopy]
    internal struct StatusStruct
    {
        public int StatusCode;

        public string Detail;

        public Exception? DebugException;
    }
}
