// <copyright file="CustomLog4NetLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomLog4NetLogProvider : Log4NetLogProvider, ILogProviderWithEnricher
    {
        private static readonly Version SupportedExtJsonAssemblyVersion = new Version("2.0.9.1");

        public ILogEnricher CreateEnricher() => new Log4NetEnricher(this);

        internal static new bool IsLoggerAvailable() =>
            Log4NetLogProvider.IsLoggerAvailable() && ExtJsonAssemblySupported();

        protected override OpenMdc GetOpenMdcMethod()
        {
            // This is a copy/paste of the base GetOpenMdcMethod, but calling Set(string, object) instead of Set(string, string)

            var logicalThreadContextType = FindType("log4net.LogicalThreadContext", "log4net");
            var propertiesProperty = logicalThreadContextType.GetProperty("Properties");
            var logicalThreadContextPropertiesType = propertiesProperty.PropertyType;
            var propertiesIndexerProperty = logicalThreadContextPropertiesType.GetProperty("Item");

            var removeMethod = logicalThreadContextPropertiesType.GetMethod("Remove");

            var keyParam = Expression.Parameter(typeof(string), "key");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var propertiesExpression = Expression.Property(null, propertiesProperty);

            // (key, value) => LogicalThreadContext.Properties.Item[key] = value;
            var setProperties =
                Expression.Assign(Expression.Property(propertiesExpression, propertiesIndexerProperty, keyParam), valueParam);

            // key => LogicalThreadContext.Properties.Remove(key);
            var removeMethodCall = Expression.Call(propertiesExpression, removeMethod, keyParam);

            var set = Expression
                .Lambda<Action<string, object>>(setProperties, keyParam, valueParam)
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

        /// <summary>
        /// <para>
        /// Dynamically loads the log4net.Ext.Json assembly and returns true 1) if the library is not found or 2) the library
        /// is found and the version is &gt;= 2.0.9.1. Otherwise, this returns false.
        /// </para>
        ///
        /// <para>
        /// Background: The log4net.Ext.Json library is a prominent third-party library that can render log4net
        /// objects into JSON. To prepare the final JSON object, the StandardTypesDecorator does a
        /// type check against each value in the MDC dictionary and tries to render each object
        /// accordingly. If the value is a well-known type, then the idiomatic JSON value is written.
        /// If not, the decorator will render the value as a new JSON object and attempt to render
        /// each of the original object's members as properties on the new JSON object.
        /// </para>
        ///
        /// <para>
        /// Bug: In log4net.Ext.Json versions &lt; 2.0.9.1, the interface log4net.Core.IFixingRequired
        /// is not included in the set of types specifically recognized as a built-in type, so the
        /// custom object Log4NetEnricher+TracerProperty that we insert into the MDC dictionary is
        /// rendered as a new JSON object, not a string, which breaks trace-log correlation.
        /// </para>
        /// </summary>
        /// <returns>true if the log4net.Ext.Json assembly is not found or if it is found and the version is &gt;= 2.0.9.1. Otherwise, false.</returns>
        private static bool ExtJsonAssemblySupported()
        {
            Assembly extJsonAssembly = Assembly.Load("log4net.Ext.Json");
            if (extJsonAssembly is not null)
            {
                return extJsonAssembly.GetName().Version >= SupportedExtJsonAssemblyVersion;
            }

            // log4net.Ext.Json not found, so there will be no compatibility issues
            return true;
        }
    }
}
