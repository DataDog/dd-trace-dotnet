using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp;

#pragma warning disable CS8500

public static class CallTargetRefStructExtensions
{
#if !NETCOREAPP3_1_OR_GREATER
    public static unsafe ref ReadOnlySpan<T> DangerousGetReadOnlySpan<T>(this CallTargetRefStruct callTargetRefStruct, out bool success)
    {
        if (callTargetRefStruct.StructType == typeof(ReadOnlySpan<T>))
        {
            success = true;
            return ref (*(ReadOnlySpan<T>*)callTargetRefStruct.Value);
        }

        success = false;
        // Null pointer (same code as Unsafe.NullRef)
        return ref (*(ReadOnlySpan<T>*)null);
    }

    public static unsafe ref Span<T> DangerousGetSpan<T>(this CallTargetRefStruct callTargetRefStruct, out bool success)
    {
        if (callTargetRefStruct.StructType == typeof(Span<T>))
        {
            success = true;
            return ref (*(Span<T>*)callTargetRefStruct.Value);
        }

        success = false;
        // Null pointer (same code as Unsafe.NullRef)
        return ref (*(Span<T>*)null);
    }
#endif

    public static unsafe ref ReadOnlyRefStruct GetReadOnlyRefStruct(this CallTargetRefStruct callTargetRefStruct, out bool success)
    {
        if (callTargetRefStruct.StructType == typeof(ReadOnlyRefStruct))
        {
            success = true;
            return ref (*(ReadOnlyRefStruct*)callTargetRefStruct.Value);
        }

        success = false;
        // Null pointer (same code as Unsafe.NullRef)
        return ref (*(ReadOnlyRefStruct*)null);
    } 
}
