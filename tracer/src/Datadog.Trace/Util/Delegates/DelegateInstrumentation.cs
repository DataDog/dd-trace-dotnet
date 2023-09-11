// <copyright file="DelegateInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Delegates;

#pragma warning disable SA1124

internal class DelegateInstrumentation
{
    public static Delegate Wrap<TCallbacks>(Delegate target, TCallbacks callbacks)
        where TCallbacks : struct, ICallbacks
        => Wrap<Delegate, TCallbacks>(target, callbacks);

    public static TDelegate Wrap<TDelegate, TCallbacks>(TDelegate target, TCallbacks callbacks)
        where TDelegate : Delegate
        where TCallbacks : struct, ICallbacks
    {
        var targetType = target.GetType();
        var invokeMethod = targetType.GetMethod("Invoke");
        var returnType = invokeMethod!.ReturnType;
        var arguments = invokeMethod!.GetParameters();
        switch (arguments.Length)
        {
            case 0:
            {
                if (returnType == typeof(void))
                {
                    var wrapperType = typeof(Action0Wrapper<,>).MakeGenericType(
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
                else
                {
                    var wrapperType = typeof(Func0Wrapper<,,>).MakeGenericType(
                        returnType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
            }

            case 1:
            {
                if (returnType == typeof(void))
                {
                    var wrapperType = typeof(Action1Wrapper<,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
                else
                {
                    var wrapperType = typeof(Func1Wrapper<,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        returnType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
            }

            case 2:
            {
                if (returnType == typeof(void))
                {
                    var wrapperType = typeof(Action2Wrapper<,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
                else
                {
                    var wrapperType = typeof(Func2Wrapper<,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        returnType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
            }

            case 3:
            {
                if (returnType == typeof(void))
                {
                    var wrapperType = typeof(Action3Wrapper<,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        arguments[2].ParameterType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
                else
                {
                    var wrapperType = typeof(Func3Wrapper<,,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        arguments[2].ParameterType,
                        returnType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
            }

            case 4:
            {
                if (returnType == typeof(void))
                {
                    var wrapperType = typeof(Action4Wrapper<,,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        arguments[2].ParameterType,
                        arguments[3].ParameterType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
                else
                {
                    var wrapperType = typeof(Func4Wrapper<,,,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        arguments[2].ParameterType,
                        arguments[3].ParameterType,
                        returnType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
            }

            case 5:
            {
                if (returnType == typeof(void))
                {
                    var wrapperType = typeof(Action5Wrapper<,,,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        arguments[2].ParameterType,
                        arguments[3].ParameterType,
                        arguments[4].ParameterType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
                else
                {
                    var wrapperType = typeof(Func5Wrapper<,,,,,,,>).MakeGenericType(
                        arguments[0].ParameterType,
                        arguments[1].ParameterType,
                        arguments[2].ParameterType,
                        arguments[3].ParameterType,
                        arguments[4].ParameterType,
                        returnType,
                        targetType,
                        typeof(TCallbacks));
                    var wrapper = (Wrapper<TCallbacks>)Activator.CreateInstance(wrapperType, target, callbacks)!;
                    return (TDelegate)wrapper.Handler;
                }
            }

            default:
                ThrowHelper.ThrowNotSupportedException("The number of parameter is not supported!");
                return default;
        }
    }

    private abstract class Wrapper<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        private Delegate? _handler;

        protected Wrapper(Delegate target, TCallbacks callbacks)
        {
            Target = target;
            Callbacks = callbacks;
        }

        public Delegate Target { get; }

        public Delegate Handler => _handler ??= GetHandler();

        public TCallbacks Callbacks { get; }

        protected abstract Delegate GetHandler();
    }

    private abstract class Wrapper<TDelegate, TCallbacks> : Wrapper<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        protected Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        protected override Delegate GetHandler()
        {
            return Delegate.CreateDelegate(typeof(TDelegate), this, "Invoke");
        }
    }

    private abstract class Wrapper<TReturn, TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        protected static readonly bool ReturnIsTask;

        static Wrapper()
        {
            var returnType = typeof(TReturn);
            var taskType = typeof(Task);
            ReturnIsTask = returnType == taskType || (returnType.IsGenericType && taskType.IsAssignableFrom(returnType));
        }

        protected Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }
    }

#region Action 0 Argument

    private class Action0Wrapper<TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin0Callbacks, IVoidReturnCallback
    {
        public Action0Wrapper(Delegate target, TCallbacks callbacks)
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

    private class Action1Wrapper<TArg, TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin1Callbacks, IVoidReturnCallback
    {
        public Action1Wrapper(Delegate target, TCallbacks callbacks)
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

    private class Action2Wrapper<TArg, TArg2, TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin2Callbacks, IVoidReturnCallback
    {
        public Action2Wrapper(Delegate target, TCallbacks callbacks)
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

    private class Action3Wrapper<TArg, TArg2, TArg3, TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin3Callbacks, IVoidReturnCallback
    {
        public Action3Wrapper(Delegate target, TCallbacks callbacks)
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

    private class Action4Wrapper<TArg, TArg2, TArg3, TArg4, TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin4Callbacks, IVoidReturnCallback
    {
        public Action4Wrapper(Delegate target, TCallbacks callbacks)
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

    private class Action5Wrapper<TArg, TArg2, TArg3, TArg4, TArg5, TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin5Callbacks, IVoidReturnCallback
    {
        public Action5Wrapper(Delegate target, TCallbacks callbacks)
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

#region Func 0 Arguments

    private class Func0Wrapper<TReturn, TDelegate, TCallbacks> : Wrapper<TReturn, TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin0Callbacks, IReturnCallback
    {
        public Func0Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke()
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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
                    returnValue = Callbacks.OnDelegateEnd(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 1 Arguments

    private class Func1Wrapper<TArg, TReturn, TDelegate, TCallbacks> : Wrapper<TReturn, TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin1Callbacks, IReturnCallback
    {
        public Func1Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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
                    returnValue = Callbacks.OnDelegateEnd(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 2 Arguments

    private class Func2Wrapper<TArg, TArg2, TReturn, TDelegate, TCallbacks> : Wrapper<TReturn, TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin2Callbacks, IReturnCallback
    {
        public Func2Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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
                    returnValue = Callbacks.OnDelegateEnd(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 3 Arguments

    private class Func3Wrapper<TArg, TArg2, TArg3, TReturn, TDelegate, TCallbacks> : Wrapper<TReturn, TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin3Callbacks, IReturnCallback
    {
        public Func3Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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
                    returnValue = Callbacks.OnDelegateEnd(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 4 Arguments

    private class Func4Wrapper<TArg, TArg2, TArg3, TArg4, TReturn, TDelegate, TCallbacks> : Wrapper<TReturn, TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin4Callbacks, IReturnCallback
    {
        public Func4Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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
                    returnValue = Callbacks.OnDelegateEnd(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

#region Func 5 Arguments

    private class Func5Wrapper<TArg, TArg2, TArg3, TArg4, TArg5, TReturn, TDelegate, TCallbacks> : Wrapper<TReturn, TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin5Callbacks, IReturnCallback
    {
        public Func5Wrapper(Delegate target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var sender = Target.Target;
            object? state = null;
            Exception? exception = null;
            TReturn? returnValue = default;
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
                    returnValue = Callbacks.OnDelegateEnd(sender, returnValue, exception, state);
                }
                catch (Exception innerException)
                {
                    Callbacks.OnException(sender, innerException);
                }
            }

            return returnValue;
        }
    }

#endregion

}
