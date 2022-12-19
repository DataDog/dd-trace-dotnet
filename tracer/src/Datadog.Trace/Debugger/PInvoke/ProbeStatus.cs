// <copyright file="ProbeStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.Debugger.PInvoke
{
    internal record ProbeStatus
    {
        public ProbeStatus(string probeId, Status status, string errorMessage = null)
        {
            ProbeId = probeId;
            ErrorMessage = errorMessage;
            Status = status;
        }

        public string ProbeId { get; }

        public string ErrorMessage { get; }

        public Status Status { get; }
    }
}
