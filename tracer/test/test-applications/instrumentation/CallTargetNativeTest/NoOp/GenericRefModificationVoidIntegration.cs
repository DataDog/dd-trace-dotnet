using System;
using Datadog.Trace.ClrProfiler.CallTarget;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

namespace CallTargetNativeTest.NoOp
{
    public static class GenericRefModificationVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 arg01)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(1)<{typeof(GenericRefModificationVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}>({instance}, {arg01})");

            if (arg01 is CallTargetRefStruct callTargetRefStruct)
            {
                ref var value = ref GetReadOnlyRefStruct(callTargetRefStruct, out var success);
                if (success)
                {
                    value = new ReadOnlyRefStruct("Hello world");
                }
            }
            
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(GenericRefModificationVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue;
        }

        public static unsafe ref ReadOnlyRefStruct GetReadOnlyRefStruct(CallTargetRefStruct callTargetRefStruct, out bool success)
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
}
