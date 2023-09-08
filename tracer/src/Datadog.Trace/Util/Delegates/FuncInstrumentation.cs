// <copyright file="FuncInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Delegates;

#pragma warning disable SA1124

internal class FuncInstrumentation
{
    public static Delegate Wrap(Delegate target, FuncCallbacks callbacks)
        => Wrap<Delegate>(target, callbacks);

    public static TDelegate Wrap<TDelegate>(TDelegate target, FuncCallbacks callbacks)
        where TDelegate : Delegate
    {
        var targetType = target.GetType();
        var invokeMethod = targetType.GetMethod("Invoke");
        var returnType = invokeMethod!.ReturnType;
        var arguments = invokeMethod!.GetParameters();
        switch (arguments.Length)
        {
            case 0:
            {
                var wrapperType = typeof(Func0Wrapper<,>).MakeGenericType(
                    returnType,
                    targetType);
                var wrapper = (FuncWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 1:
            {
                var wrapperType = typeof(Func1Wrapper<,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    returnType,
                    targetType);
                var wrapper = (FuncWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 2:
            {
                var wrapperType = typeof(Func2Wrapper<,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    returnType,
                    targetType);
                var wrapper = (FuncWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 3:
            {
                var wrapperType = typeof(Func3Wrapper<,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    returnType,
                    targetType);
                var wrapper = (FuncWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 4:
            {
                var wrapperType = typeof(Func4Wrapper<,,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    arguments[3].ParameterType,
                    returnType,
                    targetType);
                var wrapper = (FuncWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            case 5:
            {
                var wrapperType = typeof(Func5Wrapper<,,,,,,>).MakeGenericType(
                    arguments[0].ParameterType,
                    arguments[1].ParameterType,
                    arguments[2].ParameterType,
                    arguments[3].ParameterType,
                    arguments[4].ParameterType,
                    returnType,
                    targetType);
                var wrapper = (FuncWrapper)Activator.CreateInstance(wrapperType, target, callbacks)!;
                return (TDelegate)wrapper.Handler;
            }

            default:
                ThrowHelper.ThrowNotSupportedException("The number of parameter is not supported!");
                return default;
        }
    }

    private abstract class FuncWrapper
    {
        private Delegate? _handler;

        protected FuncWrapper(Delegate target, FuncCallbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
        }

        public Delegate Target { get; }

        public Delegate Handler => _handler ??= GetHandler();

        public FuncCallbacks? Callbacks { get; }

        protected abstract Delegate GetHandler();
    }

    private abstract class FuncWrapper<TReturn, TDelegate> : FuncWrapper
    {
        // ReSharper disable once StaticMemberInGenericType
        protected static readonly bool ReturnIsTask;

        static FuncWrapper()
        {
            var returnType = typeof(TReturn);
            var taskType = typeof(Task);
            ReturnIsTask = returnType == taskType || (returnType.IsGenericType && taskType.IsAssignableFrom(returnType));
        }

        protected FuncWrapper(Delegate target, FuncCallbacks? callbacks)
            : base(target, callbacks)
        {
        }

        protected override Delegate GetHandler()
        {
            return Delegate.CreateDelegate(typeof(TDelegate), this, "Invoke");
        }
    }

#region Func 0 Argument

    private class Func0Wrapper<TReturn, TDelegate> : FuncWrapper<TReturn, TDelegate>
    {
        public Func0Wrapper(Delegate target, Func0Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke()
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Func0Callbacks;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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

                returnValue = (TReturn?)Target.DynamicInvoke();
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
                    returnValue = (TReturn?)callbacks?.OnDelegateEnd?.Invoke(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 1 Argument

    private class Func1Wrapper<TArg, TReturn, TDelegate> : FuncWrapper<TReturn, TDelegate>
    {
        public Func1Wrapper(Delegate target, Func1Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Func1Callbacks;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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

                returnValue = (TReturn?)Target.DynamicInvoke(arg1);
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
                    returnValue = (TReturn?)callbacks?.OnDelegateEnd?.Invoke(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 2 Arguments

    private class Func2Wrapper<TArg, TArg2, TReturn, TDelegate> : FuncWrapper<TReturn, TDelegate>
    {
        public Func2Wrapper(Delegate target, Func2Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Func2Callbacks;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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

                returnValue = (TReturn?)Target.DynamicInvoke(arg1, arg2);
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
                    returnValue = (TReturn?)callbacks?.OnDelegateEnd?.Invoke(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 3 Arguments

    private class Func3Wrapper<TArg, TArg2, TArg3, TReturn, TDelegate> : FuncWrapper<TReturn, TDelegate>
    {
        public Func3Wrapper(Delegate target, Func3Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Func3Callbacks;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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

                returnValue = (TReturn?)Target.DynamicInvoke(arg1, arg2, arg3);
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
                    returnValue = (TReturn?)callbacks?.OnDelegateEnd?.Invoke(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 4 Arguments

    private class Func4Wrapper<TArg, TArg2, TArg3, TArg4, TReturn, TDelegate> : FuncWrapper<TReturn, TDelegate>
    {
        public Func4Wrapper(Delegate target, Func4Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Func4Callbacks;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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

                returnValue = (TReturn?)Target.DynamicInvoke(arg1, arg2, arg3, arg4);
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
                    returnValue = (TReturn?)callbacks?.OnDelegateEnd?.Invoke(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 5 Arguments

    private class Func5Wrapper<TArg, TArg2, TArg3, TArg4, TArg5, TReturn, TDelegate> : FuncWrapper<TReturn, TDelegate>
    {
        public Func5Wrapper(Delegate target, Func5Callbacks? callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var sender = Target.Target;
            var callbacks = Callbacks as Func5Callbacks;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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

                returnValue = (TReturn?)Target.DynamicInvoke(arg1, arg2, arg3, arg4, arg5);
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
                    returnValue = (TReturn?)callbacks?.OnDelegateEnd?.Invoke(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    callbacks?.OnException?.Invoke(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion
}
