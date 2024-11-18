// <copyright file="DelegateInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Delegates;

#pragma warning disable SA1124

internal static class DelegateInstrumentation
{
    public static TDelegate Wrap<TDelegate, TCallbacks>(TDelegate? target, TCallbacks callbacks)
        where TDelegate : Delegate
        where TCallbacks : struct, ICallbacks
        => (TDelegate)Wrap<TCallbacks>(target, typeof(TDelegate), callbacks);

    public static Delegate Wrap<TCallbacks>(Delegate? target, Type targetType, TCallbacks callbacks)
        where TCallbacks : struct, ICallbacks
    {
        targetType = target?.GetType() ?? targetType;
        if (targetType is null) { ThrowHelper.ThrowArgumentNullException(nameof(targetType)); }

        var activatorHelper = ActivatorHelper<TCallbacks>.Cache.GetOrAdd(
                              targetType,
                              tType =>
                              {
                                  var invokeMethod = tType.GetMethod("Invoke");
                                  var returnType = invokeMethod!.ReturnType;
                                  var arguments = invokeMethod!.GetParameters();

                                  switch (arguments.Length)
                                  {
                                      case 0:
                                      {
                                          if (returnType == typeof(void))
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Action0Wrapper<,>).MakeGenericType(
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                          else
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Func0Wrapper<,,>).MakeGenericType(
                                                                                         returnType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                      }

                                      case 1:
                                      {
                                          if (returnType == typeof(void))
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Action1Wrapper<,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                          else
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Func1Wrapper<,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         returnType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                      }

                                      case 2:
                                      {
                                          if (returnType == typeof(void))
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Action2Wrapper<,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                          else
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Func2Wrapper<,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         returnType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                      }

                                      case 3:
                                      {
                                          if (returnType == typeof(void))
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Action3Wrapper<,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         arguments[2].ParameterType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                          else
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Func3Wrapper<,,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         arguments[2].ParameterType,
                                                                                         returnType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                      }

                                      case 4:
                                      {
                                          if (returnType == typeof(void))
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Action4Wrapper<,,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         arguments[2].ParameterType,
                                                                                         arguments[3].ParameterType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                          else
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Func4Wrapper<,,,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         arguments[2].ParameterType,
                                                                                         arguments[3].ParameterType,
                                                                                         returnType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                      }

                                      case 5:
                                      {
                                          if (returnType == typeof(void))
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Action5Wrapper<,,,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         arguments[2].ParameterType,
                                                                                         arguments[3].ParameterType,
                                                                                         arguments[4].ParameterType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                          else
                                          {
                                              return new ActivatorHelper<TCallbacks>(typeof(Func5Wrapper<,,,,,,,>).MakeGenericType(
                                                                                         arguments[0].ParameterType,
                                                                                         arguments[1].ParameterType,
                                                                                         arguments[2].ParameterType,
                                                                                         arguments[3].ParameterType,
                                                                                         arguments[4].ParameterType,
                                                                                         returnType,
                                                                                         tType,
                                                                                         typeof(TCallbacks)));
                                          }
                                      }

                                      default:
                                          ThrowHelper.ThrowNotSupportedException("The number of parameter is not supported!");
                                          return default;
                                  }
                              });

        var wrapper = activatorHelper.CreateInstance(target, callbacks);
        return wrapper.Handler;
    }

    private class ActivatorHelper<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        private static readonly ConcurrentDictionary<Type, ActivatorHelper<TCallbacks>> WrapperTypesCache = new();
        private readonly Type _wrapperType;
        private Func<Delegate?, TCallbacks, Wrapper<TCallbacks>> _activator;

        public ActivatorHelper(Type wrapperType)
        {
            _wrapperType = wrapperType;
            _activator = DefaultActivator;
            Task.Run(CreateCustomActivator);
        }

        public static ConcurrentDictionary<Type, ActivatorHelper<TCallbacks>> Cache => WrapperTypesCache;

        public Wrapper<TCallbacks> CreateInstance(Delegate? target, TCallbacks callbacks)
            => _activator(target, callbacks);

        private Wrapper<TCallbacks> DefaultActivator(Delegate? target, TCallbacks callbacks)
            => (Wrapper<TCallbacks>)Activator.CreateInstance(_wrapperType, target, callbacks)!;

        private void CreateCustomActivator()
        {
            try
            {
                var ctor = _wrapperType.GetConstructors()[0];
                var createHeadersMethod = new DynamicMethod(
                    $"TypeActivator" + _wrapperType.Name,
                    typeof(Wrapper<TCallbacks>),
                    new[] { typeof(object), typeof(Delegate), typeof(TCallbacks) },
                    typeof(ActivatorHelper).Module,
                    true);

                var il = createHeadersMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
                _activator = (Func<Delegate?, TCallbacks, Wrapper<TCallbacks>>)createHeadersMethod.CreateDelegate(typeof(Func<Delegate?, TCallbacks, Wrapper<TCallbacks>>), _wrapperType);
            }
            catch (Exception ex)
            {
                // This method is only called once, so no point saving the logger in the static field forever
                // If we throw, then _activator will continue to have the default activator which is fine
                DatadogLogging.GetLoggerFor<ActivatorHelper>()
                              .Warning(ex, "Error creating the custom activator for: {Type}", typeof(TCallbacks).FullName);
            }
        }
    }

    private abstract class Wrapper<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        private Delegate? _handler;

        protected Wrapper(Delegate? target, TCallbacks callbacks)
        {
            Target = target;
            Callbacks = callbacks;
        }

        public Delegate? Target { get; }

        public Delegate Handler => _handler ??= GetHandler();

        public TCallbacks Callbacks { get; }

        protected abstract Delegate GetHandler();
    }

    private abstract class Wrapper<TDelegate, TCallbacks> : Wrapper<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        protected Wrapper(Delegate? target, TCallbacks callbacks)
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
        private static readonly Type? ReturnInnerType;

        static Wrapper()
        {
            var returnType = typeof(TReturn);
            var taskType = typeof(Task);
            if (returnType == taskType)
            {
                ReturnInnerType = null;
                var method = typeof(Wrapper<TReturn, TDelegate, TCallbacks>).GetMethod(nameof(ProcessContinuation), BindingFlags.Static | BindingFlags.NonPublic);
                method = method?.MakeGenericMethod(ReturnInnerType ?? typeof(object));
                if (method is not null)
                {
                    SetContinuation = (SetContinuationDelegate)method.CreateDelegate(typeof(SetContinuationDelegate));
                }
            }
            else if (returnType.IsGenericType && taskType.IsAssignableFrom(returnType))
            {
                ReturnInnerType = returnType.GenericTypeArguments[0];
                var method = typeof(Wrapper<TReturn, TDelegate, TCallbacks>).GetMethod(nameof(ProcessContinuation), BindingFlags.Static | BindingFlags.NonPublic);
                method = method?.MakeGenericMethod(ReturnInnerType ?? typeof(object));
                if (method is not null)
                {
                    SetContinuation = (SetContinuationDelegate)method.CreateDelegate(typeof(SetContinuationDelegate));
                }
            }
        }

        protected Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
        }

        protected delegate TReturn? SetContinuationDelegate(TCallbacks callbacks, object? sender, Exception? exception, object? state, TReturn? returnValue);

        protected static SetContinuationDelegate? SetContinuation { get; }

        private static TReturn? ProcessContinuation<TInnerReturn>(TCallbacks callbacks, object? sender, Exception? exception, object? state, TReturn? returnValue)
        {
            if (callbacks is IReturnAsyncCallback returnAsyncCallback)
            {
                if (ReturnInnerType is null)
                {
                    returnValue = (TReturn)(object)AddContinuationToSimpleTask((Task?)(object?)returnValue, returnAsyncCallback);
                }
                else
                {
                    returnValue = (TReturn)(object)AddContinuation((Task<TInnerReturn>?)(object?)returnValue, returnAsyncCallback);
                }
            }

            return returnValue;

            async Task AddContinuationToSimpleTask(Task? originalTask, IReturnAsyncCallback asyncCallback)
            {
                if (originalTask is not null)
                {
                    if (!originalTask.IsCompleted)
                    {
                        await new NoThrowAwaiter(originalTask, asyncCallback.PreserveAsyncContext);
                    }

                    if (originalTask.Status == TaskStatus.Faulted)
                    {
                        exception ??= originalTask.Exception?.GetBaseException();
                    }
                    else if (originalTask.Status == TaskStatus.Canceled)
                    {
                        try
                        {
                            // The only supported way to extract the cancellation exception is to await the task
                            await originalTask.ConfigureAwait(asyncCallback.PreserveAsyncContext);
                        }
                        catch (Exception ex)
                        {
                            exception ??= ex;
                        }
                    }
                }

                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    await asyncCallback.OnDelegateEndAsync(sender, (object?)null, exception, state).ConfigureAwait(asyncCallback.PreserveAsyncContext);
                }
                catch (Exception ex)
                {
                    asyncCallback.OnException(sender, ex);
                }

                // *
                // If the original task throws an exception we rethrow it here.
                // *
                if (exception != null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }
            }

            async Task<TInnerReturn> AddContinuation(Task<TInnerReturn>? originalTask, IReturnAsyncCallback asyncCallback)
            {
                TInnerReturn? taskResult = default;
                if (originalTask is not null)
                {
                    if (!originalTask.IsCompleted)
                    {
                        await new NoThrowAwaiter(originalTask, asyncCallback.PreserveAsyncContext);
                    }

                    if (originalTask.Status == TaskStatus.RanToCompletion)
                    {
                        taskResult = originalTask.Result;
                    }
                    else if (originalTask.Status == TaskStatus.Faulted)
                    {
                        exception ??= originalTask.Exception?.GetBaseException();
                    }
                    else if (originalTask.Status == TaskStatus.Canceled)
                    {
                        try
                        {
                            // The only supported way to extract the cancellation exception is to await the task
                            await originalTask.ConfigureAwait(asyncCallback.PreserveAsyncContext);
                        }
                        catch (Exception ex)
                        {
                            exception ??= ex;
                        }
                    }
                }

                TInnerReturn? continuationResult;
                try
                {
                    // *
                    // Calls the CallTarget integration continuation, exceptions here should never bubble up to the application
                    // *
                    continuationResult = await asyncCallback.OnDelegateEndAsync(sender, taskResult!, exception, state).ConfigureAwait(asyncCallback.PreserveAsyncContext);
                }
                catch (Exception ex)
                {
                    asyncCallback.OnException(sender, ex);
                    continuationResult = taskResult;
                }

                // *
                // If the original task throws an exception we rethrow it here.
                // *
                if (exception != null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                return continuationResult!;
            }
        }
    }

#region Action 0 Argument

    private class Action0Wrapper<TDelegate, TCallbacks> : Wrapper<TDelegate, TCallbacks>
        where TCallbacks : struct, IBegin0Callbacks, IVoidReturnCallback
    {
        private readonly Action? _invokeDelegate;

        public Action0Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Action)target.Method.CreateDelegate(typeof(Action), target.Target);
            }
        }

        private void Invoke()
        {
            var sender = Target?.Target ?? this;
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

                _invokeDelegate?.Invoke();
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
        private readonly Action<TArg>? _invokeDelegate;

        public Action1Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Action<TArg>)target.Method.CreateDelegate(
                    typeof(Action<>).MakeGenericType(
                        typeof(TArg)),
                    target.Target);
            }
        }

        private void Invoke(TArg arg1)
        {
            var sender = Target?.Target ?? this;
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

                _invokeDelegate?.Invoke(arg1);
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
        private readonly Action<TArg, TArg2>? _invokeDelegate;

        public Action2Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Action<TArg, TArg2>)target.Method.CreateDelegate(
                    typeof(Action<,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2)),
                    target.Target);
            }
        }

        private void Invoke(TArg arg1, TArg2 arg2)
        {
            var sender = Target?.Target ?? this;
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

                _invokeDelegate?.Invoke(arg1, arg2);
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
        private readonly Action<TArg, TArg2, TArg3>? _invokeDelegate;

        public Action3Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Action<TArg, TArg2, TArg3>)target.Method.CreateDelegate(
                    typeof(Action<,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TArg3)),
                    target.Target);
            }
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var sender = Target?.Target ?? this;
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

                _invokeDelegate?.Invoke(arg1, arg2, arg3);
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
        private readonly Action<TArg, TArg2, TArg3, TArg4>? _invokeDelegate;

        public Action4Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Action<TArg, TArg2, TArg3, TArg4>)target.Method.CreateDelegate(
                    typeof(Action<,,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TArg3),
                        typeof(TArg4)),
                    target.Target);
            }
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var sender = Target?.Target ?? this;
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

                _invokeDelegate?.Invoke(arg1, arg2, arg3, arg4);
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
        private readonly Action<TArg, TArg2, TArg3, TArg4, TArg5>? _invokeDelegate;

        public Action5Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Action<TArg, TArg2, TArg3, TArg4, TArg5>)target.Method.CreateDelegate(
                    typeof(Action<,,,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TArg3),
                        typeof(TArg4),
                        typeof(TArg5)),
                    target.Target);
            }
        }

        private void Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var sender = Target?.Target ?? this;
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

                _invokeDelegate?.Invoke(arg1, arg2, arg3, arg4, arg5);
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
        private readonly Func<TReturn>? _invokeDelegate;

        public Func0Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Func<TReturn>)target.Method.CreateDelegate(
                    typeof(Func<>).MakeGenericType(
                        typeof(TReturn)),
                    target.Target);
            }
        }

        private TReturn? Invoke()
        {
            var sender = Target?.Target ?? this;
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

                if (_invokeDelegate is { } invokeDelegate)
                {
                    returnValue = (TReturn?)invokeDelegate();
                }
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
                    if (SetContinuation is { } setContinuation)
                    {
                        returnValue = setContinuation(Callbacks, sender, exception, state, returnValue);
                    }

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
        private readonly Func<TArg, TReturn>? _invokeDelegate;

        public Func1Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Func<TArg, TReturn>)target.Method.CreateDelegate(
                    typeof(Func<,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TReturn)),
                    target.Target);
            }
        }

        private TReturn? Invoke(TArg arg1)
        {
            var sender = Target?.Target ?? this;
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

                if (_invokeDelegate is { } invokeDelegate)
                {
                    returnValue = (TReturn?)invokeDelegate(arg1);
                }
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
                    if (SetContinuation is { } setContinuation)
                    {
                        returnValue = setContinuation(Callbacks, sender, exception, state, returnValue);
                    }

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
        private readonly Func<TArg, TArg2, TReturn>? _invokeDelegate;

        public Func2Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Func<TArg, TArg2, TReturn>)target.Method.CreateDelegate(
                    typeof(Func<,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TReturn)),
                    target.Target);
            }
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2)
        {
            var sender = Target?.Target ?? this;
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

                if (_invokeDelegate is { } invokeDelegate)
                {
                    returnValue = (TReturn?)invokeDelegate(arg1, arg2);
                }
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
                    if (SetContinuation is { } setContinuation)
                    {
                        returnValue = setContinuation(Callbacks, sender, exception, state, returnValue);
                    }

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
        private readonly Func<TArg, TArg2, TArg3, TReturn>? _invokeDelegate;

        public Func3Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Func<TArg, TArg2, TArg3, TReturn>)target.Method.CreateDelegate(
                    typeof(Func<,,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TArg3),
                        typeof(TReturn)),
                    target.Target);
            }
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3)
        {
            var sender = Target?.Target ?? this;
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

                if (_invokeDelegate is { } invokeDelegate)
                {
                    returnValue = (TReturn?)invokeDelegate(arg1, arg2, arg3);
                }
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
                    if (SetContinuation is { } setContinuation)
                    {
                        returnValue = setContinuation(Callbacks, sender, exception, state, returnValue);
                    }

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
        private readonly Func<TArg, TArg2, TArg3, TArg4, TReturn>? _invokeDelegate;

        public Func4Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Func<TArg, TArg2, TArg3, TArg4, TReturn>)target.Method.CreateDelegate(
                    typeof(Func<,,,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TArg3),
                        typeof(TArg4),
                        typeof(TReturn)),
                    target.Target);
            }
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            var sender = Target?.Target ?? this;
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

                if (_invokeDelegate is { } invokeDelegate)
                {
                    returnValue = (TReturn?)invokeDelegate(arg1, arg2, arg3, arg4);
                }
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
                    if (SetContinuation is { } setContinuation)
                    {
                        returnValue = setContinuation(Callbacks, sender, exception, state, returnValue);
                    }

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
        private readonly Func<TArg, TArg2, TArg3, TArg4, TArg5, TReturn>? _invokeDelegate;

        public Func5Wrapper(Delegate? target, TCallbacks callbacks)
            : base(target, callbacks)
        {
            if (target is not null)
            {
                _invokeDelegate = (Func<TArg, TArg2, TArg3, TArg4, TArg5, TReturn>)target.Method.CreateDelegate(
                    typeof(Func<,,,,,>).MakeGenericType(
                        typeof(TArg),
                        typeof(TArg2),
                        typeof(TArg3),
                        typeof(TArg4),
                        typeof(TArg5),
                        typeof(TReturn)),
                    target.Target);
            }
        }

        private TReturn? Invoke(TArg arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            var sender = Target?.Target ?? this;
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

                if (_invokeDelegate is { } invokeDelegate)
                {
                    returnValue = (TReturn?)invokeDelegate(arg1, arg2, arg3, arg4, arg5);
                }
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
                    if (SetContinuation is { } setContinuation)
                    {
                        returnValue = setContinuation(Callbacks, sender, exception, state, returnValue);
                    }

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
