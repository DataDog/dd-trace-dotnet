// <copyright file="PointerHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class PointerHelpers
    {
        public static Guid GetGuidFromNativePointer(long nativePointer)
        {
            var ptr = new IntPtr(nativePointer);
#if NET45
            // deprecated
            var moduleVersionId = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
#else
            // added in net451
            var moduleVersionId = Marshal.PtrToStructure<Guid>(ptr);
#endif
            return moduleVersionId;
        }
    }
}
