// <copyright file="CustomSerilogLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomSerilogLogProvider : SerilogLogProvider, ILogProviderWithEnricher
    {
        private static Func<object, IDisposable> _pushMethod;

        public CustomSerilogLogProvider()
        {
            _pushMethod = GetPush();
        }

        public IDisposable OpenContext(object enricher)
        {
            return _pushMethod(enricher);
        }

        public ILogEnricher CreateEnricher() => new SerilogEnricher(this);

        internal static Type GetLogEnricherType() => Type.GetType("Serilog.Core.ILogEventEnricher, Serilog");

        internal static new bool IsLoggerAvailable() =>
            SerilogLogProvider.IsLoggerAvailable() && GetPushMethodInfo() != null;

        private static MethodInfo GetPushMethodInfo()
        {
            var ndcContextType = Type.GetType("Serilog.Context.LogContext, Serilog");
            return ndcContextType?.GetMethod("Push", GetLogEnricherType());
        }

        private static Func<object, IDisposable> GetPush()
        {
            var pushPropertyMethod = GetPushMethodInfo();
            var enricherParam = Expression.Parameter(typeof(object), "enricher");
            var castEnricherParam = Expression.Convert(enricherParam, GetLogEnricherType());
            var pushMethodCall = Expression.Call(null, pushPropertyMethod, castEnricherParam);
            var push = Expression.Lambda<Func<object, IDisposable>>(
                    pushMethodCall,
                    enricherParam)
                .Compile();

            return push;
        }
    }
}
