// <copyright file="NativeRemoveProbeRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace.Debugger.PInvoke
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeRemoveProbeRequest
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ProbeId;

        public NativeRemoveProbeRequest(
            string probeId)
        {
            ProbeId = probeId;
        }
    }
}
