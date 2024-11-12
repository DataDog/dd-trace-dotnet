using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp;

public static class RefStructTwoParametersVoidIntegration
{
    public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TTarget instance, ref TArg1 arg01, ref TArg2 arg02)
    {
        var returnValue = CallTargetState.GetDefault();
        Console.WriteLine($"ProfilerOK: BeginMethod(2)<{typeof(RefStructTwoParametersVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}>({instance}, {arg01}, {arg02})");

        bool success;
        if (arg01 is CallTargetRefStruct callTargetRefStruct)
        {
            ref var readOnlySpanValue = ref callTargetRefStruct.DangerousGetReadOnlySpan<char>(out success);
            if (success)
            {
                readOnlySpanValue = "Hello".AsSpan();
                goto secondArgument;
            }
            
            ref var spanValue = ref callTargetRefStruct.DangerousGetSpan<char>(out success);
            if (success)
            {
                spanValue = new Span<char>(['H', 'e', 'l', 'l', 'o']);
                goto secondArgument;
            }
            
            ref var readOnlyRefStructValue = ref callTargetRefStruct.GetReadOnlyRefStruct(out success);
            if (success)
            {
                readOnlyRefStructValue = new ReadOnlyRefStruct("Hello");
                goto secondArgument;
            }
        }
        
        secondArgument:
        if (arg02 is CallTargetRefStruct callTargetRefStruct2)
        {
            ref var readOnlySpanValue = ref callTargetRefStruct2.DangerousGetReadOnlySpan<char>(out success);
            if (success)
            {
                readOnlySpanValue = "World".AsSpan();
                goto returnValue;
            }
            
            ref var spanValue = ref callTargetRefStruct2.DangerousGetSpan<char>(out success);
            if (success)
            {
                spanValue = new Span<char>(['W', 'o', 'r', 'l', 'd']);
                goto returnValue;
            }
            
            ref var readOnlyRefStructValue = ref callTargetRefStruct2.GetReadOnlyRefStruct(out success);
            if (success)
            {
                readOnlyRefStructValue = new ReadOnlyRefStruct("World");
                goto returnValue;
            }
        }
        
        returnValue:
        return returnValue;
    }

    public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        CallTargetReturn returnValue = CallTargetReturn.GetDefault();
        Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(RefStructTwoParametersVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
        return returnValue;
    }
}
