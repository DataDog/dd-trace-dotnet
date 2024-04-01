using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// CallTargetBubbleUpExceptions Integration for 0 Arguments and Void Return
    /// </summary>
    public static class CallTargetBubbleUpExceptionsIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            var returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(CallTargetBubbleUpExceptionsIntegration)}, {typeof(TTarget)}>({instance})");
            if (instance?.GetType().Name.Contains("ThrowOnBegin") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            if (instance?.GetType().Name.Contains("ThrowBubbleUpOnBegin") == true)
            {
                Console.WriteLine("Bubbleup exception about to throw.");
                throw new CallTargetBubbleUpException();
            }

            if (instance?.GetType().Name.Contains("ThrowNestedBubbleUpOnBegin") == true)
            {
                Console.WriteLine("Nested bubbleup exception about to throw.");
                throw new Exception("nested bubble up", new CallTargetBubbleUpException());
            }

            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            var returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(CallTargetBubbleUpExceptionsIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            if (instance?.GetType().Name.Contains("ThrowOnEnd") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            if (instance?.GetType().Name.Contains("ThrowBubbleUpOnEnd") == true)
            {
                Console.WriteLine("Bubbleup exception about to throw.");
                throw new CallTargetBubbleUpException();
            }

            if (instance?.GetType().Name.Contains("ThrowNestedBubbleUpOnEnd") == true)
            {
                Console.WriteLine("Nested bubbleup exception about to throw.");
                throw new Exception("nested bubble up", new CallTargetBubbleUpException());
            }

            return returnValue;
        }
    }

    public static class CallTargetBubbleUpExceptionsIntegrationAsync
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            var returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(CallTargetBubbleUpExceptionsIntegration)}, {typeof(TTarget)}>({instance})");
            if (instance?.GetType().Name.Contains("ThrowOnBegin") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            if (instance?.GetType().Name.Contains("ThrowBubbleUpOnBegin") == true)
            {
                Console.WriteLine("Bubbleup exception about to throw.");
                throw new CallTargetBubbleUpException();
            }

            if (instance?.GetType().Name.Contains("ThrowNestedBubbleUpOnBegin") == true)
            {
                Console.WriteLine("Nested bubbleup exception about to throw.");
                throw new Exception("nested bubble up", new CallTargetBubbleUpException());
            }

            return returnValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue
        {
            Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(CallTargetBubbleUpExceptionsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            if (instance.Instance?.GetType().Name.Contains("ThrowBubbleUpOnAsyncEnd") == true)
            {
                Console.WriteLine("Bubbleup exception about to throw.");
                throw new CallTargetBubbleUpException();
            }

            if (instance.Instance?.GetType().Name.Contains("ThrowNestedBubbleUpOnAsyncEnd") == true)
            {
                Console.WriteLine("Nested bubbleup exception about to throw.");
                throw new Exception("nested bubble up", new CallTargetBubbleUpException());
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
}
