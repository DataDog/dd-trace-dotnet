// <copyright file="NativeLineProbeDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Debugger.PInvoke
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeLineProbeDefinition
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ProbeId;

        public Guid MVID;

        public int MethodToken;

        public int BytecodeOffset;

        public int LineNumber;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string ProbeFilePath;

        public NativeLineProbeDefinition(
                string probeId,
                Guid mvid,
                int methodToken,
                int bytecodeOffset,
                int lineNumber,
                string probeFilePath)
        {
            ProbeId = probeId;
            MVID = mvid;
            MethodToken = methodToken;
            BytecodeOffset = bytecodeOffset;
            LineNumber = lineNumber;
            ProbeFilePath = probeFilePath;
        }
    }
}
