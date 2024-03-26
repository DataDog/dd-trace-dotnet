// <copyright file="NativeProbeStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.Debugger.PInvoke
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeProbeStatus
    {
        public IntPtr ProbeId;
        public IntPtr ErrorMessage;
        public Status Status;
    }
}
