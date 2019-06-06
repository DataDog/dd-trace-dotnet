using System.Runtime.InteropServices;

namespace Samples.WebForms.Empty
{
    public static class NativeMethods
    {
        /// <summary>
        /// Allows us to determine if the profile is attached without
        /// adding a reference to Datadog.Trace.ClrProfiler.Managed
        /// </summary>
        [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
        public static extern bool IsProfilerAttached();
    }
}
