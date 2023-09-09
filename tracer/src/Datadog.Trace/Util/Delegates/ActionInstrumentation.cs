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
    public static Delegate Wrap<TActionCallbacks>(Delegate target, TActionCallbacks callbacks)
        where TActionCallbacks : struct, IActionCallbacks
        => Wrap<Delegate, TActionCallbacks>(target, callbacks);

    public static TDelegate Wrap<TDelegate, TActionCallbacks>(TDelegate target, TActionCallbacks callbacks)
        where TDelegate : Delegate
        where TActionCallbacks : struct, IActionCallbacks
    {
        var targetType = target.GetType();
        var invokeMethod = targetType.GetMethod("Invoke");
        var arguments = invokeMethod!.GetParameters();
        switch (arguments.Length)
        {
            case 0:
            {
                var wrapperType = typeof(Action0Wrapper<,>).MakeGenericType(
                    targetType,
                    typeof(TActionCallbacks));
                var wrapper = (ActionWrapper<TActionCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 1:
            {
                var wrapperType = typeof(Action1Wrapper<,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    targetType,
                    typeof(TActionCallbacks));
                var wrapper = (ActionWrapper<TActionCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 2:
            {
                var wrapperType = typeof(Action2Wrapper<,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    targetType,
                    typeof(TActionCallbacks));
                var wrapper = (ActionWrapper<TActionCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 3:
            {
                var wrapperType = typeof(Action3Wrapper<,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    targetType,
                    typeof(TActionCallbacks));
                var wrapper = (ActionWrapper<TActionCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 4:
            {
                var wrapperType = typeof(Action4Wrapper<,,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    arguments[3].ParameterType,
                    targetType,
                    typeof(TActionCallbacks));
                var wrapper = (ActionWrapper<TActionCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 5:
            {
                var wrapperType = typeof(Action5Wrapper<,,,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    arguments[3].ParameterType,
                    arguments[4].ParameterType,
                    targetType,
                    typeof(TActionCallbacks));
                var wrapper = (ActionWrapper<TActionCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            default:
                ThrowHelper.ThrowNotSupportedException("The number of parameter is not supported!");
                return default;
        }
    }

    private abstract class ActionWrapper<TActionCallbacks>
        where TActionCallbacks : struct, IActionCallbacks
    {
        private Delegate? _handler;

        protected ActionWrapper(Delegate target, TActionCallbacks callbacks)
        {
            Target = target;
            Callbacks = callbacks;
        }

        public Delegate Target { get; }

        public Delegate Handler => _handler ??= GetHandler();

        public TActionCallbacks Callbacks { get; }

        protected abstract Delegate GetHandler();
    }

    private abstract class ActionWrapper<TDelegate, TActionCallbacks> : ActionWrapper<TActionCallbacks>
        where TActionCallbacks : struct, IActionCallbacks
    {
        protected ActionWrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        protected override Delegate GetHandler()
        {
            return Delegate.CreateDelegate(typeof(TDelegate), this, "Invoke");
        }
    }

#region Action 0 Argument

    private class Action0Wrapper<TDelegate, TActionCallbacks> : ActionWrapper<TDelegate, TActionCallbacks>
        where TActionCallbacks : struct, IAction0Callbacks
    {
        public Action0Wrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke()
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = Callbacks.OnDelegateBegin(sender);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
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
                    Callbacks.OnDelegateEnd(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 1 Argument

    private class Action1Wrapper<TArg, TDelegate, TActionCallbacks> : ActionWrapper<TDelegate, TActionCallbacks>
        where TActionCallbacks : struct, IAction1Callbacks
    {
        public Action1Wrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = Callbacks.OnDelegateBegin(sender, ref arg1);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
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
                    Callbacks.OnDelegateEnd(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 2 Arguments

    private class Action2Wrapper<TArg, TArg2, TDelegate, TActionCallbacks> : ActionWrapper<TDelegate, TActionCallbacks>
        where TActionCallbacks : struct, IAction2Callbacks
    {
        public Action2Wrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = Callbacks.OnDelegateBegin(sender, ref arg1, ref arg2);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
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
                    Callbacks.OnDelegateEnd(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 3 Arguments

    private class Action3Wrapper<TArg, TArg2, TArg3, TDelegate, TActionCallbacks> : ActionWrapper<TDelegate, TActionCallbacks>
        where TActionCallbacks : struct, IAction3Callbacks
    {
        public Action3Wrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = Callbacks.OnDelegateBegin(sender, ref arg1, ref arg2, ref arg3);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
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
                    Callbacks.OnDelegateEnd(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 4 Arguments

    private class Action4Wrapper<TArg, TArg2, TArg3, TArg4, TDelegate, TActionCallbacks> : ActionWrapper<TDelegate, TActionCallbacks>
        where TActionCallbacks : struct, IAction4Callbacks
    {
        public Action4Wrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = Callbacks.OnDelegateBegin(sender, ref arg1, ref arg2, ref arg3, ref arg4);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
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
                    Callbacks.OnDelegateEnd(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }
        }
    }

#endregion

#region Action 5 Arguments

    private class Action5Wrapper<TArg, TArg2, TArg3, TArg4, TArg5, TDelegate, TActionCallbacks> : ActionWrapper<TDelegate, TActionCallbacks>
        where TActionCallbacks : struct, IAction5Callbacks
    {
        public Action5Wrapper(Delegate target, TActionCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            try
            {
                try
                {
                    state = Callbacks.OnDelegateBegin(sender, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
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
                    Callbacks.OnDelegateEnd(sender, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }
        }
    }

#endregion
}
