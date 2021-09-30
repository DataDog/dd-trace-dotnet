// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal static readonly IDatadogLogger Log = Ci.CIVisibility.Log;

        internal static void FlushSpans(IntegrationInfo integrationInfo)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(integrationInfo))
            {
                return;
            }

            Ci.CIVisibility.FlushSpans();
        }

        internal static string GetParametersValueData(object paramValue)
        {
            if (paramValue is null)
            {
                return "(null)";
            }
            else if (paramValue is Array pValueArray)
            {
                const int maxArrayLength = 50;
                int length = pValueArray.Length > maxArrayLength ? maxArrayLength : pValueArray.Length;

                string[] strValueArray = new string[length];
                for (var i = 0; i < length; i++)
                {
                    strValueArray[i] = GetParametersValueData(pValueArray.GetValue(i));
                }

                return "[" + string.Join(", ", strValueArray) + (pValueArray.Length > maxArrayLength ? ", ..." : string.Empty) + "]";
            }
            else if (paramValue is Delegate pValueDelegate)
            {
                return $"{paramValue}[{pValueDelegate.Target}|{pValueDelegate.Method}]";
            }

            return paramValue.ToString();
        }
    }
}
