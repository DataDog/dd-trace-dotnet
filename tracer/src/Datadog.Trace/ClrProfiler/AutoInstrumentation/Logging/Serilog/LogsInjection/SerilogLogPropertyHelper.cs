// <copyright file="SerilogLogPropertyHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.LogsInjection
{
    internal static class SerilogLogPropertyHelper<TMarkerType>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Func<string, object> CreateScalarValueWith;

        static SerilogLogPropertyHelper()
        {
            // Initialize delegate for creating a MessageAttributeValue object
            var assembly = typeof(TMarkerType).Assembly;
            var scalarValueType = assembly.GetType("Serilog.Events.ScalarValue");
            var scalarValueConstructor = scalarValueType.GetConstructor(new[] { typeof(object) });

            DynamicMethod createLogEventPropertyMethod = new DynamicMethod(
                $"SerilogLogPropertyHelper",
                returnType: scalarValueType,
                parameterTypes: new Type[] { typeof(string) },
                typeof(DuckType).Module,
                true);

            ILGenerator createScalarValueIl = createLogEventPropertyMethod.GetILGenerator();

            createScalarValueIl.Emit(OpCodes.Ldarg_0); // value
            createScalarValueIl.Emit(OpCodes.Newobj, scalarValueConstructor);
            createScalarValueIl.Emit(OpCodes.Ret);

            CreateScalarValueWith = (Func<string, object>)createLogEventPropertyMethod.CreateDelegate(typeof(Func<string, object>));
        }

        public static object CreateScalarValue(string value)
        {
            return CreateScalarValueWith(value);
        }
    }
}
