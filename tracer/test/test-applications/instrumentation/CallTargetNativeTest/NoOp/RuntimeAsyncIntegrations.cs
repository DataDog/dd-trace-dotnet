extern alias DatadogTrace;
using System;
using System.Threading.Tasks;
using DatadogTrace::Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// Declares OnAsyncMethodEnd but no OnMethodEnd
    /// </summary>
    public static class RuntimeAsyncAsyncEndOnlyIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(RuntimeAsyncAsyncEndOnlyIntegration)}, {typeof(TTarget)}>({instance})");
            return CallTargetState.GetDefault();
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(RuntimeAsyncAsyncEndOnlyIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue;
        }
    }

    /// <summary>
    /// Declares OnMethodEnd but no OnAsyncMethodEnd
    /// </summary>
    public static class RuntimeAsyncMethodEndOnlyIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(RuntimeAsyncMethodEndOnlyIntegration)}, {typeof(TTarget)}>({instance})");
            return CallTargetState.GetDefault();
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Console.WriteLine($"ProfilerOK: EndMethod(1)<{typeof(RuntimeAsyncMethodEndOnlyIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }

    /// <summary>
    /// Replaces the awaited result from OnAsyncMethodEnd
    /// </summary>
    public static class RuntimeAsyncReplaceResultIntegration
    {
        public const int IntReplacement = 43;
        public const string StringReplacement = "Replaced";

        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(RuntimeAsyncReplaceResultIntegration)}, {typeof(TTarget)}>({instance})");
            return CallTargetState.GetDefault();
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(RuntimeAsyncReplaceResultIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue switch
            {
                int => (TReturn)(object)IntReplacement,
                string => (TReturn)(object)StringReplacement,
                _ => returnValue,
            };
        }
    }

    /// <summary>
    /// Replaces the returned task from OnMethodEnd with a completed one carrying a different result
    /// </summary>
    public static class RuntimeAsyncReplaceTaskIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(RuntimeAsyncReplaceTaskIntegration)}, {typeof(TTarget)}>({instance})");
            return CallTargetState.GetDefault();
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Console.WriteLine($"ProfilerOK: EndMethod(1)<{typeof(RuntimeAsyncReplaceTaskIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue switch
            {
                Task<int> => new CallTargetReturn<TReturn>((TReturn)(object)Task.FromResult(RuntimeAsyncReplaceResultIntegration.IntReplacement)),
                ValueTask<int> => new CallTargetReturn<TReturn>((TReturn)(object)new ValueTask<int>(RuntimeAsyncReplaceResultIntegration.IntReplacement)),
                _ => new CallTargetReturn<TReturn>(returnValue),
            };
        }
    }
}
