// <copyright file="CachedBasicPropertiesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

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
        il.Emit(OpCodes.Ldarg_0);                  // Load first argument (object).
        il.Emit(OpCodes.Castclass, parameterType); // Cast to IReadOnlyBasicProperties
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Ret);
        Activator = (Func<object, object>)createBasicPropertiesMethod.CreateDelegate(typeof(Func<object, object>), targetType);
    }

    public static TBasicProperties CreateHeaders(object readonlyBasicProperties)
        => (TBasicProperties)Activator(readonlyBasicProperties);
}
