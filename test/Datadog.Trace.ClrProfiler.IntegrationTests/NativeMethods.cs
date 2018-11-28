using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    internal static class NativeMethods
    {
        [DllImport("User32.Dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);
    }
}
