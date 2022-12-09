// <copyright file="TaskContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class TaskContinuationGenerator<TIntegration, TTarget, TReturn> : ContinuationGenerator<TTarget, TReturn>
    {
        private static readonly ContinuationMethodDelegate _continuation;
        private static readonly AsyncContinuationMethodDelegate _asyncContinuation;
        private static readonly bool _preserveContext;

        static TaskContinuationGenerator()
        {
            var result = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
            if (result.Method is not null)
            {
                if (result.Method.ReturnType == typeof(Task))
                {
                    _asyncContinuation = (AsyncContinuationMethodDelegate)result.Method.CreateDelegate(typeof(AsyncContinuationMethodDelegate));
                }
                else if (result.Method.ReturnType.IsGenericType && typeof(Task).IsAssignableFrom(result.Method.ReturnType))
                {
                    _asyncContinuation = (AsyncContinuationMethodDelegate)result.Method.CreateDelegate(typeof(AsyncContinuationMethodDelegate));
                }
                else
                {
                    _continuation = (ContinuationMethodDelegate)result.Method.CreateDelegate(typeof(ContinuationMethodDelegate));
                }

                _preserveContext = result.PreserveContext;
            }
        }

        internal delegate object ContinuationMethodDelegate(TTarget target, object returnValue, Exception exception, in CallTargetState state);

        internal delegate Task<object> AsyncContinuationMethodDelegate(TTarget target, object returnValue, Exception exception, in CallTargetState state);

        public override TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (_continuation is not null)
            {
                if (exception != null || returnValue == null)
                {
                    _continuation(instance, returnValue, exception, in state);
                    return returnValue;
                }

                var previousTask = FromTReturn<Task>(returnValue);
                if (previousTask.Status == TaskStatus.RanToCompletion)
                {
                    _continuation(instance, returnValue, exception, in state);
                    return returnValue;
                }

                return ToTReturn(ContinuationAction(previousTask, instance, state));
            }

            if (_asyncContinuation is not null)
            {
                var previousTask = returnValue == null ? null : FromTReturn<Task>(returnValue);
                return ToTReturn(ContinuationAction(previousTask, instance, state));
            }

            return returnValue;
        }

        private static async Task ContinuationAction(Task previousTask, TTarget target, CallTargetState state)
        {
            Exception exception = null;

            if (previousTask is not null)
            {
                if (!previousTask.IsCompleted)
                {
                    await new NoThrowAwaiter(previousTask, _preserveContext);
                }

                if (previousTask.Status == TaskStatus.Faulted)
                {
                    exception = previousTask.Exception?.GetBaseException();
                }
                else if (previousTask.Status == TaskStatus.Canceled)
                {
                    try
                    {
                        // The only supported way to extract the cancellation exception is to await the task
                        await previousTask.ConfigureAwait(_preserveContext);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                }
            }

            try
            {
                // *
                // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                // *
                if (_continuation is not null)
                {
                    _continuation(target, null, exception, in state);
                }
                else if (_asyncContinuation is not null)
                {
                    await _asyncContinuation(target, null, exception, in state).ConfigureAwait(_preserveContext);
                }
            }
            catch (Exception ex)
            {
                IntegrationOptions<TIntegration, TTarget>.LogException(ex, "Exception occurred when calling the CallTarget integration continuation.");
            }

            // *
            // If the original task throws an exception we rethrow it here.
            // *
            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }
}
