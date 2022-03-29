// <copyright file="DdwafResultStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DdwafResultStruct
    {
        public bool Timeout;

        public int PerfTotalRuntime;

        public IntPtr Data;

        public IntPtr PerfData;
    }
}
