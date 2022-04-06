// <copyright file="IStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    /// <summary>
    /// Duck type for Grpc.Core.Status
    /// Same as <see cref="StatusStruct"/>, but an interface for use in constraints
    /// </summary>
    internal interface IStatus
    {
        public int StatusCode { get; }

        public string Detail { get; }

        public Exception DebugException { get; }
    }
}
