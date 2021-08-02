// <copyright file="PWRet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PWRet
    {
        public PW_RET_CODE Action;

        public IntPtr Data;

        public IntPtr PerfData;

        public int PerfTotalRuntime;

        public int PerfCacheHitRate;
    }
}
