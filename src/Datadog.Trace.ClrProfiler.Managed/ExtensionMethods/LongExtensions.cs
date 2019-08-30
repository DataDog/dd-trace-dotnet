using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class LongExtensions
    {
        public static Guid GetGuidFromNativePointer(this long nativePointer)
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
