using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Helper class to simulate the calltarget rewriting for benchmark tests
    /// </summary>
    internal unsafe static class CallTarget
    {
        // ***************************************************************************************************************
        //  Run
        // ***************************************************************************************************************

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TResult>(TTarget targetInstance, delegate*<TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget>(targetInstance);
                }
                catch(Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback();
            }
            catch(Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TResult>(TTarget targetInstance, TArg1 arg1, delegate*<TArg1, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1>(targetInstance, arg1);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, delegate*<TArg1, TArg2, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2>(targetInstance, arg1, arg2);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TArg3, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, delegate*<TArg1, TArg2, TArg3, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3>(targetInstance, arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, delegate*<TArg1, TArg2, TArg3, TArg4, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(targetInstance, arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(targetInstance, arg1, arg2, arg3, arg4, arg5);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2, arg3, arg4, arg5);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(targetInstance, arg1, arg2, arg3, arg4, arg5, arg6);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2, arg3, arg4, arg5, arg6);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(targetInstance, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResult Run<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TResult>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TResult> bodyCallback)
        {
            TResult result = default;
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn<TResult> cReturn = CallTargetReturn<TResult>.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(targetInstance, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                result = bodyCallback(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget, TResult>(targetInstance, result, exception, state);
                    result = cReturn.GetReturnValue();
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
            return result;
        }

        // ***************************************************************************************************************
        //  RunVoid
        // ***************************************************************************************************************

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget>(TTarget targetInstance, delegate*<void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget>(targetInstance);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1>(TTarget targetInstance, TArg1 arg1, delegate*<TArg1, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1>(targetInstance, arg1);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, delegate*<TArg1, TArg2, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2>(targetInstance, arg1, arg2);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2, TArg3>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, delegate*<TArg1, TArg2, TArg3, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3>(targetInstance, arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2, arg3);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, delegate*<TArg1, TArg2, TArg3, TArg4, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(targetInstance, arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2, arg3, arg4);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(targetInstance, arg1, arg2, arg3, arg4, arg5);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2, arg3, arg4, arg5);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(targetInstance, arg1, arg2, arg3, arg4, arg5, arg6);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2, arg3, arg4, arg5, arg6);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(targetInstance, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunVoid<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(TTarget targetInstance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, delegate*<TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, void> bodyCallback)
        {
            CallTargetState state = CallTargetState.GetDefault();
            CallTargetReturn cReturn = CallTargetReturn.GetDefault();
            Exception exception = null;
            try
            {
                try
                {
                    state = CallTargetInvoker.BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(targetInstance, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
                bodyCallback(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                try
                {
                    cReturn = CallTargetInvoker.EndMethod<TIntegration, TTarget>(targetInstance, exception, state);
                }
                catch (Exception ex)
                {
                    CallTargetInvoker.LogException<TIntegration, TTarget>(ex);
                    throw;
                }
            }
        }
    }
}
