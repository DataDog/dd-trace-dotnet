using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp;

public static class RefStructOneParametersVoidIntegration
{
    public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 arg01)
    {
        var returnValue = CallTargetState.GetDefault();
        Console.WriteLine($"ProfilerOK: BeginMethod(1)<{typeof(RefStructOneParametersVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}>({instance}, {arg01})");

        bool success;
        if (arg01 is CallTargetRefStruct callTargetRefStruct)
        {
            ref var readOnlySpanValue = ref callTargetRefStruct.DangerousGetReadOnlySpan<char>(out success);
            if (success)
            {
                readOnlySpanValue = "Hello World".AsSpan();
                return returnValue;
            }
            
            ref var spanValue = ref callTargetRefStruct.DangerousGetSpan<char>(out success);
            if (success)
            {
                spanValue = new Span<char>(['H', 'e', 'l', 'l', 'o', ' ', 'W', 'o', 'r', 'l', 'd']);
                return returnValue;
            }
            
            ref var readOnlyRefStructValue = ref callTargetRefStruct.GetReadOnlyRefStruct(out success);
            if (success)
            {
                readOnlyRefStructValue = new ReadOnlyRefStruct("Hello World");
                return returnValue;
            }
        }
            
        return returnValue;
    }

    public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        CallTargetReturn returnValue = CallTargetReturn.GetDefault();
        Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(RefStructOneParametersVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
        return returnValue;
    }
}
