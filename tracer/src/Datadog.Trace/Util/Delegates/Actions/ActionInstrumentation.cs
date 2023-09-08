// <copyright file="ActionInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util.Delegates.Actions;

#pragma warning disable SA1124

internal class ActionInstrumentation
{
    public static Delegate? Wrap(Delegate target, ActionCallbacks callbacks)
    {
        var targetType = target.GetType();
        var invokeMethod = targetType.GetMethod("Invoke");
        var arguments = invokeMethod!.GetParameters();
        switch (arguments.Length)
        {
            case 0:
            {
                var wrapperType = typeof(Action0Wrapper<>).MakeGenericType(
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return wrapper.Handler;
            }

            case 1:
            {
                var wrapperType = typeof(Action1Wrapper<,>).MakeGenericType(
                    arguments[0].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return wrapper.Handler;
            }

            case 2:
            {
                var wrapperType = typeof(Action2Wrapper<,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return wrapper.Handler;
            }

            case 3:
            {
                var wrapperType = typeof(Action3Wrapper<,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return wrapper.Handler;
            }

            case 4:
            {
                var wrapperType = typeof(Action4Wrapper<,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    arguments[3].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return wrapper.Handler;
            }

            case 5:
            {
                var wrapperType = typeof(Action5Wrapper<,,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    arguments[3].ParameterType,
                    arguments[4].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return wrapper.Handler;
            }

            default:
                ThrowHelper.ThrowNotSupportedException("The number of parameter is not supported!");
                return null;
        }
    }

    private abstract class ActionWrapper
    {
        public Delegate? Target { get; protected set; }

        public Delegate? Handler { get; protected set; }

        public ActionCallbacks? Callbacks { get; protected set; }
    }

#region Action 0 Argument

    private class Action0Wrapper<TDelegateType> : ActionWrapper
        where TDelegateType : Delegate
    {
        public Action0Wrapper(Delegate target, Action0Callbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private void Invoke()
        {
            var target = Target;
            var callbacks = Callbacks as Action0Callbacks;

            if (target is null)
            {
                return;
            }

            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(target.Target);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }

                target.DynamicInvoke();
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
                    callbacks?.OnDelegateEnd?.Invoke(target.Target, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }
            }
        }
    }

#endregion

#region Action 1 Argument

    private class Action1Wrapper<TArg, TDelegateType> : ActionWrapper
        where TDelegateType : Delegate
    {
        public Action1Wrapper(Delegate target, Action1Callbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private void Invoke(TArg arg1)
        {
            var target = Target;
            var callbacks = Callbacks as Action1Callbacks;

            if (target is null)
            {
                return;
            }

            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(target.Target, arg1);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }

                target.DynamicInvoke(arg1);
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
                    callbacks?.OnDelegateEnd?.Invoke(target.Target, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }
            }
        }
    }

#endregion

#region Action 2 Arguments

    private class Action2Wrapper<TArg, TArg2, TDelegateType> : ActionWrapper
        where TDelegateType : Delegate
    {
        public Action2Wrapper(Delegate target, Action2Callbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private void Invoke(TArg arg1, TArg2 arg2)
        {
            var target = Target;
            var callbacks = Callbacks as Action2Callbacks;

            if (target is null)
            {
                return;
            }

            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(target.Target, arg1, arg2);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }

                target.DynamicInvoke(arg1, arg2);
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
                    callbacks?.OnDelegateEnd?.Invoke(target.Target, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }
            }
        }
    }

#endregion

#region Action 3 Arguments

    private class Action3Wrapper<TArg, TArg2, TArg3, TDelegateType> : ActionWrapper
        where TDelegateType : Delegate
    {
        public Action3Wrapper(Delegate target, Action3Callbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var target = Target;
            var callbacks = Callbacks as Action3Callbacks;

            if (target is null)
            {
                return;
            }

            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(target.Target, arg1, arg2, arg3);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }

                target.DynamicInvoke(arg1, arg2, arg3);
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
                    callbacks?.OnDelegateEnd?.Invoke(target.Target, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }
            }
        }
    }

#endregion

#region Action 4 Arguments

    private class Action4Wrapper<TArg, TArg2, TArg3, TArg4, TDelegateType> : ActionWrapper
        where TDelegateType : Delegate
    {
        public Action4Wrapper(Delegate target, Action4Callbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var target = Target;
            var callbacks = Callbacks as Action4Callbacks;

            if (target is null)
            {
                return;
            }

            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(target.Target, arg1, arg2, arg3, arg4);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }

                target.DynamicInvoke(arg1, arg2, arg3, arg4);
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
                    callbacks?.OnDelegateEnd?.Invoke(target.Target, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }
            }
        }
    }

#endregion

#region Action 5 Arguments

    private class Action5Wrapper<TArg, TArg2, TArg3, TArg4, TArg5, TDelegateType> : ActionWrapper
        where TDelegateType : Delegate
    {
        public Action5Wrapper(Delegate target, Action5Callbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var target = Target;
            var callbacks = Callbacks as Action5Callbacks;

            if (target is null)
            {
                return;
            }

            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(target.Target, arg1, arg2, arg3, arg4, arg5);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }

                target.DynamicInvoke(arg1, arg2, arg3, arg4);
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
                    callbacks?.OnDelegateEnd?.Invoke(target.Target, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(target.Target, innerException);
                }
            }
        }
    }

#endregion
}
