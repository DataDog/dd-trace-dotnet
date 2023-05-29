// <copyright file="CachedMessageHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal static class CachedMessageHeadersHelper<TMarkerType>
    {
        private static readonly Func<object> _activator;

        static CachedMessageHeadersHelper()
        {
            var headersType = typeof(TMarkerType).Assembly.GetType("Confluent.Kafka.Headers");

            ConstructorInfo ctor = headersType.GetConstructor(System.Type.EmptyTypes);

            DynamicMethod createHeadersMethod = new DynamicMethod(
                $"KafkaCachedMessageHeadersHelpers",
                headersType,
                new[] { typeof(object) },
                typeof(DuckType).Module,
                true);

            ILGenerator il = createHeadersMethod.GetILGenerator();
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ret);

            _activator = (Func<object>)createHeadersMethod.CreateInstanceDelegate(typeof(Func<object>));
        }

        /// <summary>
        /// Creates a Confluent.Kafka.Headers object and assigns it to an `IMessage` proxy
        /// </summary>
        /// <returns>A proxy for the new Headers object</returns>
        public static IHeaders CreateHeaders()
        {
            var headers = _activator();
            return headers.DuckCast<IHeaders>();
        }
    }
}
