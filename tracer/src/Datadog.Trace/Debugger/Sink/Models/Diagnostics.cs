// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Sink.Models;

internal record Diagnostics
{
    public Diagnostics(string probeId, Status status, Exception exception)
    {
        ProbeId = probeId;
        Status = status;

        Exception = exception == null ? null : new ProbeException()
        {
            Type = exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace
        };
    }

    public Diagnostics(string probeId, Status status, string errorMessage)
    {
        ProbeId = probeId;
        Status = status;

        Exception = new ProbeException()
        {
            Message = errorMessage,
        };
    }

    public string ProbeId { get; set; }

    public Status Status { get; set; }

    public ProbeException Exception { get; set; }
}
