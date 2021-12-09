// <copyright file="FallbackNLogLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    /// <summary>
    /// <para>
    /// Log provider that enhances the built-in LibLog NLogLogProvider by adding
    /// MDC support for NLog 1.0. The built-in NLogLogProvider only looked for
    /// API's present on NLog 2.0 and newer.
    /// </para>
    ///
    /// <para>
    /// Note: This logger is intended to be used when the application uses NLog &lt; 4.1.
    /// When the application uses NLog versions 4.1 and newer, use
    /// <see cref="CustomNLogLogProvider"/> which utilizes the Set(string, object)
    /// API to perform logs injection more efficiently.
    /// </para>
    /// </summary>
    internal class FallbackNLogLogProvider : NLogLogProvider
    {
        protected override OpenMdc GetOpenMdcMethod()
        {
            // This is a copy/paste of the base GetOpenMdcMethod, with the NLog 4.1+ code path removed and an additional NLog 1.x fallback
            var keyParam = Expression.Parameter(typeof(string), "key");

            var mdcContextType = FindType("NLog.MappedDiagnosticsContext", "NLog");
            if (mdcContextType is null)
            {
                // Modification: Add fallback for NLog version 1.x
                mdcContextType = FindType("NLog.MDC", "NLog");
            }

            var setMethod = mdcContextType.GetMethod("Set", typeof(string), typeof(string));
            var removeMethod = mdcContextType.GetMethod("Remove", typeof(string));
            var valueParam = Expression.Parameter(typeof(string), "value");
            var setMethodCall = Expression.Call(null, setMethod, keyParam, valueParam);
            var removeMethodCall = Expression.Call(null, removeMethod, keyParam);

            var set = Expression
                .Lambda<Action<string, string>>(setMethodCall, keyParam, valueParam)
                .Compile();
            var remove = Expression
                .Lambda<Action<string>>(removeMethodCall, keyParam)
                .Compile();

            return (key, value, _) =>
            {
                set(key, value.ToString());
                return new DisposableAction(() => remove(key));
            };
        }
    }
}
