using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp;

public static class RefStructFourParametersVoidIntegration
{
    public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string arg01, ref CallTargetRefStruct arg02, ref CallTargetRefStruct arg03, ref CallTargetRefStruct arg04)
    {
        var returnValue = CallTargetState.GetDefault();
        Console.WriteLine($"ProfilerOK: BeginMethod(4)<{typeof(RefStructFourParametersVoidIntegration)}, {typeof(TTarget)}>({instance}, {arg01}, {arg02}, {arg03}, {arg04})");

        bool success;
        arg01 = "Hello";

        ref var readOnlySpanValue = ref arg02.DangerousGetReadOnlySpan<char>(out success);
        if (success)
        {
            readOnlySpanValue = "World".AsSpan();
        }

        ref var spanValue = ref arg03.DangerousGetSpan<char>(out success);
        if (success)
        {
            spanValue = new Span<char>(['H', 'e', 'l', 'l', 'o']);
        }

        ref var readOnlyRefStructValue = ref arg04.GetReadOnlyRefStruct(out success);
        if (success)
        {
            readOnlyRefStructValue = new ReadOnlyRefStruct("World");
        }
        
        return returnValue;
    }

    public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        CallTargetReturn returnValue = CallTargetReturn.GetDefault();
        Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(RefStructFourParametersVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
        return returnValue;
    }
}
