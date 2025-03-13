// <copyright file="MeterListenerDelegatesBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.OTelMetrics.DuckTypes;
using Datadog.Trace.Util;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListenerDelegatesBuilder
    {
        public static Delegate InstrumentPublishedDelegate(Type meterListenerType, Type instrumentType, MethodInfo onInstrumentPublishedMethodInfo)
        {
            // (instrument, listener) =>
            // {
            //    if (instrument.Meter.Name == allowedName)
            //    {
            //        listener.EnableMeasurementEvents(instrument);
            //    }
            // }
            var dynMethod = new DynamicMethod(
                "InstrumentPublishedDyn",
                typeof(void),
                new[] { instrumentType, meterListenerType },
                typeof(MeterListener).Module,
                true);

            var instrumentProxyResult = DuckType.GetOrCreateProxyType(typeof(IInstrument), instrumentType);
            var instrumentProxyType = instrumentProxyResult.ProxyType;
            if (instrumentProxyType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {instrumentType} is null");
            }

            var meterListenerProxyResult = DuckType.GetOrCreateProxyType(typeof(IInstrument), meterListenerType);
            var meterListenerProxyType = meterListenerProxyResult.ProxyType;
            if (meterListenerProxyType is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {meterListenerType} is null");
            }

            var method = onInstrumentPublishedMethodInfo.MakeGenericMethod(instrumentProxyType, meterListenerProxyType);
            var instrumentProxyTypeCtor = instrumentProxyType.GetConstructors()[0];
            var meterListenerProxyTypeCtor = meterListenerProxyType.GetConstructors()[0];

            var il = dynMethod.GetILGenerator();

            // Load the instrument argument first
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, instrumentProxyTypeCtor);

            // Load the meter argument next
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, meterListenerProxyTypeCtor);

            // Invoke our static method
            il.EmitCall(OpCodes.Call, method, null);
            il.Emit(OpCodes.Ret);

            return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(new[] { instrumentType, meterListenerType }));
        }
    }
}
#endif
