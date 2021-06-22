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
        private static Func<object, IDisposable> _pushMethod;
        private readonly bool _wrapEnricher;

        public CustomSerilogLogProvider()
        {
            var logEnricherType = GetLogEnricherType();
            if (GetPushMethodInfo() is MethodInfo pushMethodInfo)
            {
                _wrapEnricher = false;
                _pushMethod = GeneratePushDelegate(pushMethodInfo, logEnricherType);
            }
            else if (GetPushPropertiesMethodInfo() is MethodInfo pushPropertiesMethodInfo)
            {
                _wrapEnricher = true;
                _pushMethod = GeneratePushDelegate(pushPropertiesMethodInfo, logEnricherType.MakeArrayType());
            }
            else
            {
                _wrapEnricher = false;
                IDisposable cachedDisposable = new NoOpDisposable();
                _pushMethod = (enricher) => { return cachedDisposable; };
            }
        }

        public IDisposable OpenContext(object enricher)
        {
            return _pushMethod(enricher);
        }

        public ILogEnricher CreateEnricher() => new SerilogEnricher(this, _wrapEnricher);

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

        internal class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
                // Do nothing
            }
        }
    }
}
