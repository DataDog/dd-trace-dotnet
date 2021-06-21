// <copyright file="CustomSerilogLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomSerilogLogProvider : SerilogLogProvider, ILogProviderWithEnricher
    {
        private static ConditionalWeakTable<object, object> _enricherToArrayConditionalWeakTable;
        private static Func<object, IDisposable> _pushMethod;
        private static Func<object, IDisposable> _openContext;

        public CustomSerilogLogProvider()
        {
            _enricherToArrayConditionalWeakTable = new();

            var logEnricherType = GetLogEnricherType();
            if (GetPushMethodInfo() != null)
            {
                _openContext = _pushMethod = GeneratePushDelegate(GetPushMethodInfo(), logEnricherType);
            }
            else
            {
                _pushMethod = GeneratePushDelegate(GetPushPropertiesMethodInfo(), logEnricherType.MakeArrayType());
                _openContext = (enricher) =>
                {
                    // Use a conditional weak table to cache a map between the enricher and an array of size 1 containing the enricher,
                    // since the API requires an argument of type ILogEventEnricher[]
                    object properties = _enricherToArrayConditionalWeakTable.GetValue(key: enricher, createValueCallback: ConditionalWeakTableCallback);
                    return _pushMethod(properties);
                };
            }
        }

        public IDisposable OpenContext(object enricher) => _openContext(enricher);

        public ILogEnricher CreateEnricher() => new SerilogEnricher(this);

        internal static Type GetLogEnricherType() => Type.GetType("Serilog.Core.ILogEventEnricher, Serilog");

        internal static new bool IsLoggerAvailable() =>
            SerilogLogProvider.IsLoggerAvailable() && (GetPushMethodInfo() != null || GetPushPropertiesMethodInfo() != null);

        private static MethodInfo GetPushMethodInfo()
        {
            var ndcContextType = Type.GetType("Serilog.Context.LogContext, Serilog");
            return ndcContextType?.GetMethod("Push", GetLogEnricherType());
        }

        private static MethodInfo GetPushPropertiesMethodInfo()
        {
            var ndcContextType = FindType("Serilog.Context.LogContext", new[] { "Serilog", "Serilog.FullNetFx" });
            return ndcContextType?.GetMethod("PushProperties", GetLogEnricherType().MakeArrayType());
        }

        private static Func<object, IDisposable> GeneratePushDelegate(MethodInfo methodInfo, Type argumentTargetType)
        {
            var enricherParam = Expression.Parameter(typeof(object), "enricher");
            var castEnricherParam = Expression.Convert(enricherParam, argumentTargetType);
            var pushMethodCall = Expression.Call(null, methodInfo, castEnricherParam);

            var push = Expression.Lambda<Func<object, IDisposable>>(
                    pushMethodCall,
                    enricherParam)
                .Compile();

            return push;
        }

        private static object ConditionalWeakTableCallback(object key)
        {
            var array = Array.CreateInstance(GetLogEnricherType(), 1);
            array.SetValue(key, 0);
            return array;
        }
    }
}
