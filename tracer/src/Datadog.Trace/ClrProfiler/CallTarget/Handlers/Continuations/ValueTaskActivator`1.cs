// <copyright file="ValueTaskActivator`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal static class ValueTaskActivator<TValueTask, TResult>
{
    private static readonly Func<Task<TResult>, TValueTask> TaskActivator;
    private static readonly Func<TResult, TValueTask> ResultActivator;

    static ValueTaskActivator()
    {
        try
        {
            TaskActivator = CreateTaskActivator();
            ResultActivator = CreateResultActivator();
        }
        catch (Exception ex)
        {
            DatadogLogging.GetLoggerFor<ActivatorHelper>()
                          .Error(ex, "Error creating the custom activator for: {Type}", typeof(TValueTask).FullName);

            // Unfortunately this will box the ValueTask, but I think it's still the best we can do in this scenario
            TaskActivator = FallbackTaskActivator;
            ResultActivator = FallbackResultActivator;
        }
    }

    [TestingAndPrivateOnly]
    internal static Func<Task<TResult>, TValueTask> CreateTaskActivator()
    {
        var valueTaskType = typeof(TValueTask);
        var ctor = valueTaskType.GetConstructor([typeof(Task<TResult>)])!;

        var createValueTaskMethod = new DynamicMethod(
            $"TypeActivatorTask" + valueTaskType.Name,
            returnType: valueTaskType,
            parameterTypes: [typeof(Task<TResult>)],
            typeof(DuckType).Module,
            true);

        var il = createValueTaskMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Ret);

        return (Func<Task<TResult>, TValueTask>)createValueTaskMethod.CreateDelegate(typeof(Func<Task<TResult>, TValueTask>));
    }

    [TestingAndPrivateOnly]
    internal static Func<TResult, TValueTask> CreateResultActivator()
    {
        var valueTaskType = typeof(TValueTask);
        var ctor = valueTaskType.GetConstructor([typeof(TResult)])!;

        var createValueTaskMethod = new DynamicMethod(
            $"TypeActivatorResult" + valueTaskType.Name,
            returnType: valueTaskType,
            parameterTypes: [typeof(TResult)],
            typeof(DuckType).Module,
            true);

        var il = createValueTaskMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Ret);

        return (Func<TResult, TValueTask>)createValueTaskMethod.CreateDelegate(typeof(Func<TResult, TValueTask>));
    }

    [TestingAndPrivateOnly]
    internal static TValueTask FallbackTaskActivator(Task<TResult> task)
        => (TValueTask)Activator.CreateInstance(typeof(TValueTask), task)!;

    internal static TValueTask FallbackResultActivator(TResult task)
        => (TValueTask)Activator.CreateInstance(typeof(TValueTask), task)!;

    public static TValueTask CreateInstance(Task<TResult> task) => TaskActivator(task);

    public static TValueTask CreateInstance(TResult result) => ResultActivator(result);
}
#endif
