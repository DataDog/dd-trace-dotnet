// <copyright file="DelegateWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

/// <summary>
/// DelegateWrapper can be used to wrap a Delegate with callbacks that get invoked before and after the Delegate.
/// Currently supports wrapping Delegate LambdaBootstrapHandlerDelegateWrapper
/// </summary>
internal class DelegateWrapper
{
    private DelegateWrapper()
    {
        Handler = null!;
    }

    public Delegate? Target { get; protected set; }

    public Delegate Handler { get; protected set; }

    public WrapperCallbacks? Callbacks { get; protected set; }

    public static DelegateWrapper Wrap(Delegate delegateInstance, WrapperCallbacks callbacks)
    {
        var delegateType = delegateInstance.GetType();
        var invokeMethod = delegateType.GetMethod("Invoke");
        var returnType = invokeMethod!.ReturnType.GetGenericArguments()[0];
        var argumentType = invokeMethod.GetParameters()[0].ParameterType;
        var callDelegateWrapper = typeof(LambdaBootstrapHandlerDelegateWrapper<,,>).MakeGenericType(argumentType, returnType, delegateType);
        return (DelegateWrapper)Activator.CreateInstance(callDelegateWrapper, delegateInstance, callbacks)!;
    }

    internal class WrapperCallbacks
    {
        public Action<object?, object?>? BeforeDelegate { get; set; }

        public Func<object?, object?, object, Exception?, object>? AfterDelegate { get; set; }

        public Action<object?, object?, object?, Exception?>? AfterDelegateAsync { get; set; }
    }

    private class LambdaBootstrapHandlerDelegateWrapper<TArg, TReturn, TDelegateType> : DelegateWrapper
        where TDelegateType : Delegate
    {
        public LambdaBootstrapHandlerDelegateWrapper(Delegate target, WrapperCallbacks? callbacks)
        {
            Target = target;
            Callbacks = callbacks;
            Handler = (TDelegateType)Delegate.CreateDelegate(typeof(TDelegateType), this, nameof(Invoke));
        }

        private Task<TReturn> Invoke(TArg argument)
        {
            var result = SyncInvoke(argument);

            try
            {
                _ = SetAsyncCallback(argument, result);
            }
            catch (Exception ex)
            {
                Serverless.Debug($"Debug level AfterDelegateAsync exception. {ex.Message}");
            }

            return result;
        }

        private async Task SetAsyncCallback(TArg argument, Task<TReturn> asyncTask)
        {
            try
            {
                var asyncResult = await asyncTask.ConfigureAwait(false);
                Callbacks?.AfterDelegateAsync?.Invoke(Target?.Target, argument, asyncResult, null);
            }
            catch (Exception ex)
            {
                Callbacks?.AfterDelegateAsync?.Invoke(Target?.Target, argument, null, ex);
            }
        }

        private Task<TReturn> SyncInvoke(TArg argument)
        {
            if (Target is null)
            {
                return default!;
            }

            object? response = null;
            Exception? exception = null;
            try
            {
                try
                {
                    Callbacks?.BeforeDelegate?.Invoke(Target.Target, argument);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss:fff} Exception running BeforeDelegate: {e?.ToString().Replace("\n", "\\n")}");
                }

                response = Target.DynamicInvoke(argument);
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                try
                {
                    response = Callbacks?.AfterDelegate?.Invoke(Target.Target, argument, (TReturn)response!, exception);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss:fff} Exception running AfterDelegate: {e?.ToString().Replace("\n", "\\n")}");
                }
            }

            return (Task<TReturn>)response!;
        }
    }
}
