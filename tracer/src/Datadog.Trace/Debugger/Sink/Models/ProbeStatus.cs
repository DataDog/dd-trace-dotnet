// <copyright file="ProbeStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Sink.Models;

internal record ProbeStatus
{
    public ProbeStatus(string service, string probeId, Status status, Exception exception = null, string errorMessage = null)
    {
        Message = GetMessage();
        Service = service;

        Diagnostics = new Diagnostics(probeId, status);

        if (status == Status.ERROR)
        {
            Diagnostics.SetException(exception, errorMessage);
        }

        string GetMessage()
        {
            return status switch
            {
                Status.RECEIVED => $"Received probe {probeId}.",
                Status.INSTALLED => $"Installed probe {probeId}.",
                Status.BLOCKED => $"Blocked probe {probeId}.",
                Status.ERROR => $"Error installing probe {probeId}.",
                _ => throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}"),
            };
        }
    }

    public string DdSource { get; } = "dd_debugger";

    public string Service { get; }

    public string Message { get; }

    public Diagnostics Diagnostics { get; }
}
