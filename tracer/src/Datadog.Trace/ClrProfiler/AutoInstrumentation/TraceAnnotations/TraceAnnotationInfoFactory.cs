// <copyright file="TraceAnnotationInfoFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations
{
    internal static class TraceAnnotationInfoFactory
    {
        public static TraceAnnotationInfo Create(MethodBase? method)
        {
            if (method is null)
            {
                return TraceAnnotationInfo.Default;
            }
            else
            {
                return new TraceAnnotationInfo(resourceName: method.Name);
            }
        }
    }
}
