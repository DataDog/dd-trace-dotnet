// <copyright file="IDebuggerSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.Debugger.Sink
{
    internal interface IDebuggerSink : IDisposable
    {
        Task StartFlushingAsync();

        void AddSnapshot(string probeId, string snapshot);

        void AddProbeStatus(string probeId, Status status, Exception exception = null, string errorMessage = null);
    }

    internal static class DebuggerSinkExtensions
    {
        internal static void AddReceivedProbeStatus(this IDebuggerSink debuggerSink, string probeId)
        {
            debuggerSink.AddProbeStatus(probeId, Status.RECEIVED);
        }

        internal static void AddInstalledProbeStatus(this IDebuggerSink debuggerSink, string probeId)
        {
            debuggerSink.AddProbeStatus(probeId, Status.INSTALLED);
        }

        internal static void AddBlockedProbeStatus(this IDebuggerSink debuggerSink, string probeId)
        {
            debuggerSink.AddProbeStatus(probeId, Status.BLOCKED);
        }

        internal static void AddErrorProbeStatus(this IDebuggerSink debuggerSink, string probeId, Exception exception, string errorMessage)
        {
            debuggerSink.AddProbeStatus(probeId, Status.ERROR, exception, errorMessage);
        }
    }
}
