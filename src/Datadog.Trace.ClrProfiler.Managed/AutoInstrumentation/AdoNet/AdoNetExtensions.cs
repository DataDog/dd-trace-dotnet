using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    internal static class AdoNetExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DisposeWithException(this Scope scope, Exception exception)
        {
            if (scope != null)
            {
                if (exception != null)
                {
                    scope.Span.SetException(exception);
                }

                scope.Dispose();
            }
        }
    }
}
