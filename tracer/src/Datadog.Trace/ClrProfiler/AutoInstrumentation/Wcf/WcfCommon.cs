// <copyright file="WcfCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Reflection;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    internal class WcfCommon
    {
        private static readonly Lazy<Func<object>> _getCurrentOperationContext = new Lazy<Func<object>>(CreateGetCurrentOperationContextDelegate, isThreadSafe: true);

        internal const string IntegrationName = nameof(IntegrationIds.Wcf);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        public static Func<object> GetCurrentOperationContext => _getCurrentOperationContext.Value;

        private static Func<object> CreateGetCurrentOperationContextDelegate()
        {
            var operationContextType = Type.GetType("System.ServiceModel.OperationContext, System.ServiceModel, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", throwOnError: false);
            if (operationContextType is not null)
            {
                var property = operationContextType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                var method = property.GetGetMethod();
                return (Func<object>)method.CreateDelegate(typeof(Func<object>));
            }

            return null;
        }
    }
}
#endif
