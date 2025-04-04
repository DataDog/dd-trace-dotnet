using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp;

/// <summary>
/// CallTargetBubbleUpExceptions Integration for 0 Arguments and Void Return
/// </summary>
public static class InstrumentationExceptionsIntegration
{
    public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        var returnValue = CallTargetState.GetDefault();
        Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(InstrumentationExceptionsIntegration)}, {typeof(TTarget)}>({instance})");
        var name = instance?.GetType().Name;
        if (name?.Contains("ThrowOnBegin") == true)
        {
            Console.WriteLine("Exception thrown.");
            if (name.Contains("DuckTypeException"))
            {
                DuckTypeException.Throw("Throwing a ducktype exception");
            }
            else if (name.Contains("CallTargetInvokerException"))
            {
                throw new CallTargetInvokerException(new Exception("Throwing a call target invoker exception"));
            }
            else if (name.Contains("MissingMethodException"))
            {
                throw new MissingMethodException("Throwing a missing method exception");
            }
        }

        return returnValue;
    }

    public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        var returnValue = CallTargetReturn.GetDefault();
        Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(InstrumentationExceptionsIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
        var name = instance?.GetType().Name;
        if (name?.Contains("ThrowOnEnd") == true)
        {
            Console.WriteLine("Exception thrown.");
            if (name.Contains("DuckTypeException"))
            {
                DuckTypeException.Throw("Throwing a ducktype exception");
            }
            else if (name.Contains("CallTargetInvokerException"))
            {
                throw new CallTargetInvokerException(new Exception("Throwing a call target invoker exception"));
            }
            else if (name.Contains("MissingMethodException"))
            {
                throw new MissingMethodException("Throwing a missing method exception");
            }
        }

        return returnValue;
    }
}


public static class InstrumentationExceptionsIntegrationAsync
{
    public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        var returnValue = CallTargetState.GetDefault();
        Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(InstrumentationExceptionsIntegrationAsync)}, {typeof(TTarget)}>({instance})");
        var name = instance?.GetType().Name;
        if (name?.Contains("ThrowOnBegin") == true)
        {
            Console.WriteLine("Exception thrown.");
            if (name.Contains("DuckTypeException"))
            {
                DuckTypeException.Throw("Throwing a ducktype exception");
            }
            else if (name.Contains("CallTargetInvokerException"))
            {
                throw new CallTargetInvokerException(new Exception("Throwing a call target invoker exception"));
            }
            else if (name.Contains("MissingMethodException"))
            {
                throw new MissingMethodException("Throwing a missing method exception");
            }
        }

        return returnValue;
    }

    public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TTarget : IInstance, IDuckType
        where TReturn : IReturnValue
    {
        Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(InstrumentationExceptionsIntegrationAsync)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
        var name = instance.Instance?.GetType().Name;
        if (name?.Contains("ThrowBubbleUpOnAsyncEnd") == true)
        {
            Console.WriteLine("Exception thrown.");
            if (name.Contains("DuckTypeException"))
            {
                DuckTypeException.Throw("Throwing a ducktype exception");
            }
            else if (name.Contains("CallTargetInvokerException"))
            {
                throw new CallTargetInvokerException(new Exception("Throwing a call target invoker exception"));
            }
            else if (name.Contains("MissingMethodException"))
            {
                throw new MissingMethodException("Throwing a missing method exception");
            }
        }

        return returnValue;
    }

    public interface IInstance
    {
    }

    public interface IArg
    {
    }

    public interface IReturnValue
    {
    }
}
