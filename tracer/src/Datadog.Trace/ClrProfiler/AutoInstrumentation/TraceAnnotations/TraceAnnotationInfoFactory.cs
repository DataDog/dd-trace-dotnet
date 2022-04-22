// <copyright file="TraceAnnotationInfoFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations
{
    internal static class TraceAnnotationInfoFactory
    {
        private const string TraceAttributeFullName = "Datadog.Trace.Annotations.TraceAttribute";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TraceAnnotationInfoFactory));

        public static TraceAnnotationInfo Create(MethodBase? method)
        {
            if (method is null)
            {
                return TraceAnnotationInfo.Default;
            }
            else
            {
                string defaultResourceName = method.DeclaringType is null ? method.Name : method.DeclaringType!.Name + "." + method.Name;
                var attributes = method.GetCustomAttributes(true);
                foreach (var attr in attributes)
                {
                    if (attr.GetType() is Type attrType && attrType.FullName == TraceAttributeFullName)
                    {
                        try
                        {
                            string resourceName = attrType.GetProperty("ResourceName")?.GetValue(attr) as string ?? defaultResourceName;
                            string operationName = attrType.GetProperty("OperationName")?.GetValue(attr) as string ?? TraceAnnotationInfo.DefaultOperationName;
                            return new TraceAnnotationInfo(resourceName, operationName);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Unable to access properties on type {AssemblyQualifiedName}", attrType.AssemblyQualifiedName);
                        }
                    }
                }

                return new TraceAnnotationInfo(resourceName: defaultResourceName, operationName: TraceAnnotationInfo.DefaultOperationName);
            }
        }
    }
}
