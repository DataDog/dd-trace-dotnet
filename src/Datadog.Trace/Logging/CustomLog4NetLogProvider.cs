// <copyright file="CustomLog4NetLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq.Expressions;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomLog4NetLogProvider : Log4NetLogProvider, ILogProviderWithEnricher
    {
        public ILogEnricher CreateEnricher() => new Log4NetEnricher(this);

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
    }
}
