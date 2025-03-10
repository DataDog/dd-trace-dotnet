// <copyright file="CachedBasicPropertiesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

internal static class CachedBasicPropertiesHelper<TBasicProperties>
{
    private static readonly Func<object, TBasicProperties> Activator;

    static CachedBasicPropertiesHelper()
    {
        var targetType = typeof(TBasicProperties).Assembly.GetType("RabbitMQ.Client.BasicProperties")!;
        var parameterType = typeof(TBasicProperties).Assembly.GetType("RabbitMQ.Client.IReadOnlyBasicProperties")!;

        var constructor = targetType.GetConstructor([parameterType])!;

        var createBasicPropertiesMethod = new DynamicMethod(
            $"TypeActivator_{targetType.Name}_{parameterType.Name}",
            targetType,
            [typeof(object)],
            typeof(DuckType).Module,
            skipVisibility: true);

        var il = createBasicPropertiesMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);                  // Load first argument (as object).
        il.Emit(OpCodes.Castclass, parameterType); // Cast to IReadOnlyBasicProperties
        il.Emit(OpCodes.Newobj, constructor);      // Call constructor
        il.Emit(OpCodes.Ret);                      // Return new instance

        // typeof(Func<object, BasicProperties>)
        var funcType = typeof(Func<,>).MakeGenericType(typeof(object), targetType);

        Activator = (Func<object, TBasicProperties>)createBasicPropertiesMethod.CreateDelegate(funcType);
    }

    public static TBasicProperties CreateHeaders(object readonlyBasicProperties)
        => Activator(readonlyBasicProperties);
}
