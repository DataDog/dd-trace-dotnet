using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.ClrProfiler
{
    internal static class NativeMethods
    {
        [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
        public static extern void GetMetadataNames(
            [In] IntPtr moduleId,
            uint methodToken,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder wszModulePath,
            ulong cchModulePath,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder wszTypeDefName,
            ulong cchTypeDefName,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder wszMethodDefName,
            ulong cchMethodDefName);

        [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
        public static extern bool IsProfilerAttached();
    }
}
