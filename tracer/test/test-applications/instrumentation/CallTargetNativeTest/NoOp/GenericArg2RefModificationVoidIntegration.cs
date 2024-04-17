using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp;

#pragma warning disable CS8500
public static class GenericArg2RefModificationVoidIntegration
{
    public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TTarget instance, ref TArg1 arg01, ref TArg2 arg02)
    {
        CallTargetState returnValue = CallTargetState.GetDefault();
        Console.WriteLine($"ProfilerOK: BeginMethod(2)<{typeof(GenericArg2RefModificationVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}>({instance}, {arg01}, {arg02})");

        if (arg01 is CallTargetRefStruct firstNameRefStruct &&
            arg02 is CallTargetRefStruct lastNameRefStruct)
        {
            ref var firstName = ref firstNameRefStruct.GetReadOnlySpan<char>(out var firstNameSuccess);
            ref var lastName = ref lastNameRefStruct.GetReadOnlySpan<char>(out var lastNameSuccess);
            
            if (firstNameSuccess && lastNameSuccess)
            {
#if NETCOREAPP3_1_OR_GREATER
                firstName = "Hello";
                lastName = "World";
#else
                firstName = new Datadog.Trace.VendoredMicrosoftCode.System.ReadOnlySpan<char>("Hello".ToCharArray());
                lastName = new Datadog.Trace.VendoredMicrosoftCode.System.ReadOnlySpan<char>("World".ToCharArray());
#endif
            }
        }

        return returnValue;
    }

    public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        CallTargetReturn returnValue = CallTargetReturn.GetDefault();
        Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(GenericArg2RefModificationVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
        return returnValue;
    }
}
