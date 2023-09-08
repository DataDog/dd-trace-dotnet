// <copyright file="ActionInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util.Delegates;

#pragma warning disable SA1124

internal class ActionInstrumentation
{
    public static Delegate Wrap(Delegate target, ActionCallbacks callbacks)
        => Wrap<Delegate>(target, callbacks);

    public static TDelegate Wrap<TDelegate>(TDelegate target, ActionCallbacks callbacks)
        where TDelegate : Delegate
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
                return (TDelegate)wrapper.Handler;
            }

            case 1:
            {
                var wrapperType = typeof(Action1Wrapper<,>).MakeGenericType(
                    arguments[0].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 2:
            {
                var wrapperType = typeof(Action2Wrapper<,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 3:
            {
                var wrapperType = typeof(Action3Wrapper<,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    targetType);
                var wrapper = (ActionWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
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
                return (TDelegate)wrapper.Handler;
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
                return (TDelegate)wrapper.Handler;
            }

            default:
                ThrowHelper.ThrowNotSupportedException("The number of parameter is not supported!");
                return default;
        }
    }

    private abstract class ActionWrapper
    {
        private Delegate? _handler;

        protected ActionWrapper(Delegate target, ActionCallbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
        }

        public Delegate Target { get; }

        public Delegate Handler => _handler ??= GetHandler();

        public ActionCallbacks? Callbacks { get; }

        protected abstract Delegate GetHandler();
    }

    private abstract class ActionWrapper<TDelegate> : ActionWrapper
    {
        protected ActionWrapper(Delegate target, ActionCallbacks? callbacks)
            : base(target, callbacks)
        {
        }

        protected override Delegate GetHandler()
        {
            return Delegate.CreateDelegate(typeof(TDelegate), this, "Invoke");
        }
    }

#region Action 0 Argument

    private class Action0Wrapper<TDelegate> : ActionWrapper<TDelegate>
    {
        public Action0Wrapper(Delegate target, Action0Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke()
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Action0Callbacks;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(sender);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }

                Target.DynamicInvoke();
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
                    callbacks?.OnDelegateEnd?.Invoke(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 1 Argument

    private class Action1Wrapper<TArg, TDelegate> : ActionWrapper<TDelegate>
    {
        public Action1Wrapper(Delegate target, Action1Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Action1Callbacks;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(sender, arg1);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }

                Target.DynamicInvoke(arg1);
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
                    callbacks?.OnDelegateEnd?.Invoke(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 2 Arguments

    private class Action2Wrapper<TArg, TArg2, TDelegate> : ActionWrapper<TDelegate>
    {
        public Action2Wrapper(Delegate target, Action2Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Action2Callbacks;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(sender, arg1, arg2);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }

                Target.DynamicInvoke(arg1, arg2);
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
                    callbacks?.OnDelegateEnd?.Invoke(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 3 Arguments

    private class Action3Wrapper<TArg, TArg2, TArg3, TDelegate> : ActionWrapper<TDelegate>
    {
        public Action3Wrapper(Delegate target, Action3Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Action3Callbacks;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(sender, arg1, arg2, arg3);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }

                Target.DynamicInvoke(arg1, arg2, arg3);
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
                    callbacks?.OnDelegateEnd?.Invoke(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 4 Arguments

    private class Action4Wrapper<TArg, TArg2, TArg3, TArg4, TDelegate> : ActionWrapper<TDelegate>
    {
        public Action4Wrapper(Delegate target, Action4Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Action4Callbacks;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }

                Target.DynamicInvoke(arg1, arg2, arg3, arg4);
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
                    callbacks?.OnDelegateEnd?.Invoke(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 5 Arguments

    private class Action5Wrapper<TArg, TArg2, TArg3, TArg4, TArg5, TDelegate> : ActionWrapper<TDelegate>
    {
        public Action5Wrapper(Delegate target, Action5Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Action5Callbacks;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = callbacks?.OnDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4, arg5);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }

                Target.DynamicInvoke(arg1, arg2, arg3, arg4, arg5);
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
                    callbacks?.OnDelegateEnd?.Invoke(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }
        }
    }

#endregion
}
