// <copyright file="ProbeStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Sink.Models
{
    internal sealed record ProbeStatus
    {
        public ProbeStatus(string service, string probeId, Status status, int probeVersion = 0, Exception exception = null, string errorMessage = null)
        {
            Message = GetMessage();
            Service = service;
            DebuggerDiagnostics = new DebuggerDiagnostics(new Diagnostics(probeId, status, probeVersion));

            if (status == Status.ERROR)
            {
                DebuggerDiagnostics.Diagnostics.SetException(exception, errorMessage);
            }

            string GetMessage()
            {
                return status switch
                {
                    Status.RECEIVED => $"Received probe {probeId}.",
                    Status.INSTALLED => $"Installed probe {probeId}.",
                    Status.EMITTING => $"Emitted probe {probeId}.",
                    Status.BLOCKED => $"Blocked probe {probeId}.",
                    Status.ERROR => $"Error installing probe {probeId}.",
                    _ => throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}"),
                };
            }
        }

        [JsonProperty("ddsource")]
        public string DdSource { get; } = DebuggerTags.DDSource;

        [JsonProperty("service")]
        public string Service { get; }

        [JsonProperty("message")]
        public string Message { get; }

        [JsonProperty("debugger.type")]
        public string DebuggerType { get; } = DebuggerTags.DebuggerType.Diagnostic;

        // in .net tracer, this is always "di" for now
        [JsonProperty("debugger.product")]
        public string DebuggerProduct { get; } = DebuggerTags.DebuggerProduct.DI;

        [JsonProperty("debugger")]
        public DebuggerDiagnostics DebuggerDiagnostics { get; }
    }
}
