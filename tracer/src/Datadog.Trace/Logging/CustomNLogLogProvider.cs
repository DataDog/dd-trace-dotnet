// <copyright file="CustomNLogLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomNLogLogProvider : NLogLogProvider, ILogProviderWithEnricher
    {
        public ILogEnricher CreateEnricher() => new LogEnricher(this);

        protected override OpenMdc GetOpenMdcMethod()
        {
            // This is a copy/paste of the base GetOpenMdcMethod, but calling Set(string, object) instead of Set(string, string)

            var keyParam = Expression.Parameter(typeof(string), "key");

            var ndlcContextType = FindType("NLog.NestedDiagnosticsLogicalContext", "NLog");
            if (ndlcContextType != null)
            {
                var pushObjectMethod = ndlcContextType.GetMethod("PushObject", typeof(object));
                if (pushObjectMethod != null)
                {
                    // NLog 4.6 introduces SetScoped with correct handling of logical callcontext (MDLC)
                    var mdlcContextType = FindType("NLog.MappedDiagnosticsLogicalContext", "NLog");
                    if (mdlcContextType != null)
                    {
                        var setScopedMethod = mdlcContextType.GetMethod("SetScoped", typeof(string), typeof(object));
                        if (setScopedMethod != null)
                        {
                            var valueObjParam = Expression.Parameter(typeof(object), "value");
                            var setScopedMethodCall = Expression.Call(null, setScopedMethod, keyParam, valueObjParam);
                            var setMethodLambda = Expression.Lambda<Func<string, object, IDisposable>>(setScopedMethodCall, keyParam, valueObjParam).Compile();
                            return (key, value, _) => setMethodLambda(key, value);
                        }
                    }
                }
            }

            var mdcContextType = FindType("NLog.MappedDiagnosticsContext", "NLog");
            var setMethod = mdcContextType.GetMethod("Set", typeof(string), typeof(object));
            var removeMethod = mdcContextType.GetMethod("Remove", typeof(string));
            var valueParam = Expression.Parameter(typeof(object), "value");
            var setMethodCall = Expression.Call(null, setMethod, keyParam, valueParam);
            var removeMethodCall = Expression.Call(null, removeMethod, keyParam);

            var set = Expression
                .Lambda<Action<string, object>>(setMethodCall, keyParam, valueParam)
                .Compile();
            var remove = Expression
                .Lambda<Action<string>>(removeMethodCall, keyParam)
                .Compile();

            return (key, value, _) =>
            {
                set(key, value);
                return new DisposableAction(() => remove(key));
            };
        }
    }
}
