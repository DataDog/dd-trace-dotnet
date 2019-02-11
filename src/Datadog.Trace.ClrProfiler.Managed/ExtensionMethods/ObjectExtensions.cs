using System;

namespace Datadog.Trace.ClrProfiler.ExtensionMethods
{
    internal static class ObjectExtensions
    {
        internal static void TryDispose(this IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Ignore disposal exceptions here...
            }
        }
    }
}
