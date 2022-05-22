// <copyright file="LineProbeResolveResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Models
{
    internal record LineProbeResolveResult
    {
        public LineProbeResolveResult(LiveProbeResolveStatus status, string message = null)
        {
            Status = status;
            Message = message;
        }

        public LiveProbeResolveStatus Status { get; }

        public string Message { get; }
    }
}
