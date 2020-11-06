using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class EndMethodHandler<TIntegration, TTarget, TReturn>
    {
        private static readonly InvokeDelegate _invokeDelegate = null;
        private static readonly ContinuationGenerator<TIntegration, TTarget, TReturn> _continuationGenerator;

        static EndMethodHandler()
        {
            Type returnType = typeof(TReturn);
            try
            {
                DynamicMethod dynMethod = IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget), returnType);
                if (dynMethod != null)
                {
                    _invokeDelegate = (InvokeDelegate)dynMethod.CreateDelegate(typeof(InvokeDelegate));
                }
            }
            catch (Exception ex)
            {
                throw new CallTargetInvokerException(ex);
            }

            Type resultType = typeof(void);

            if (returnType.IsGenericType)
            {
                resultType = ContinuationsHelper.GetResultType(returnType);

                Type genericReturnType = returnType.GetGenericTypeDefinition();
                if (typeof(Task).IsAssignableFrom(returnType))
                {
                    // The type is a Task<>
                    _continuationGenerator = (ContinuationGenerator<TIntegration, TTarget, TReturn>)Activator.CreateInstance(typeof(TaskContinuationGenerator<,,,>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, resultType));
                }
#if NETCOREAPP3_1 || NET5_0
                else if (genericReturnType == typeof(ValueTask<>))
                {
                    // The type is a ValueTask<>
                    _continuationGenerator = (ContinuationGenerator<TIntegration, TTarget, TReturn>)Activator.CreateInstance(typeof(ValueTaskContinuationGenerator<,,,>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, resultType));
                }
#endif
            }
            else
            {
                if (returnType == typeof(Task))
                {
                    // The type is a Task
                    _continuationGenerator = new TaskContinuationGenerator<TIntegration, TTarget, TReturn>();
                }
#if NETCOREAPP3_1 || NET5_0
                else if (returnType == typeof(ValueTask))
                {
                    // The type is a ValueTask
                    _continuationGenerator = new ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn>();
                }
#endif
            }

            _continuationGenerator ??= new ContinuationGenerator<TIntegration, TTarget, TReturn>();
        }

        internal delegate CallTargetReturn<TReturn> InvokeDelegate(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetReturn<TReturn> Invoke(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            if (_invokeDelegate != null)
            {
                CallTargetReturn<TReturn> returnWrap = _invokeDelegate(instance, returnValue, exception, state);
                returnValue = returnWrap.GetReturnValue();
            }

            if (returnValue != null)
            {
                returnValue = _continuationGenerator.SetContinuation(instance, returnValue, exception, state);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
