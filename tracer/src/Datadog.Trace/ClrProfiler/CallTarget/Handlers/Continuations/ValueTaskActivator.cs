// <copyright file="ValueTaskActivator.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal static class ValueTaskActivator<TValueTask>
{
    private static readonly Func<Task, TValueTask> Activator;

    static ValueTaskActivator()
    {
        try
        {
            Activator = CreateActivator();
        }
        catch (Exception ex)
        {
            DatadogLogging.GetLoggerFor<ActivatorHelper>()
                          .Error(ex, "Error creating the custom activator for: {Type}", typeof(TValueTask).FullName);

            // Unfortunately this will box the ValueTask, but I think it's still the best we can do in this scenario
            Activator = FallbackActivator;
        }
    }

    [TestingAndPrivateOnly]
    internal static Func<Task, TValueTask> CreateActivator()
    {
        var valueTaskType = typeof(TValueTask);
        var ctor = valueTaskType.GetConstructor([typeof(Task)])!;

        var createValueTaskMethod = new DynamicMethod(
            $"TypeActivator" + valueTaskType.Name,
            returnType: valueTaskType,
            parameterTypes: [typeof(Task)],
            typeof(DuckType).Module,
            true);

        var il = createValueTaskMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Ret);

        return (Func<Task, TValueTask>)createValueTaskMethod.CreateDelegate(typeof(Func<Task, TValueTask>));
    }

    [TestingAndPrivateOnly]
    internal static TValueTask FallbackActivator(Task task)
        => (TValueTask)System.Activator.CreateInstance(typeof(TValueTask), task)!;

    public static TValueTask CreateInstance(Task task) => Activator(task);
}
#endif
