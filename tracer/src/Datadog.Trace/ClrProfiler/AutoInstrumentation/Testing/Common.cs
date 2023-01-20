// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal static readonly IDatadogLogger Log = Ci.CIVisibility.Log;

        internal static string GetParametersValueData(object paramValue)
        {
            if (paramValue is null)
            {
                return "(null)";
            }

            if (paramValue is string strValue)
            {
                return strValue;
            }

            if (paramValue is Array pValueArray)
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

            if (paramValue is Delegate pValueDelegate)
            {
                return $"{paramValue}[{pValueDelegate.Target}|{pValueDelegate.Method}]";
            }

            return paramValue.ToString();
        }

        internal static bool ShouldSkip(string testSuite, string testName, object[] testMethodArguments, ParameterInfo[] methodParameters)
        {
            var currentContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                var skippableTests = AsyncUtil.RunSync(() => CIVisibility.GetSkippableTestsFromSuiteAndNameAsync(testSuite, testName));
                if (skippableTests.Count > 0)
                {
                    foreach (var skippableTest in skippableTests)
                    {
                        var parameters = skippableTest.GetParameters();

                        // Same test name and no parameters
                        if ((parameters?.Arguments is null || parameters.Arguments.Count == 0) &&
                            (testMethodArguments is null || testMethodArguments.Length == 0))
                        {
                            return true;
                        }

                        if (parameters?.Arguments is not null)
                        {
                            var matchSignature = true;
                            for (var i = 0; i < methodParameters.Length; i++)
                            {
                                var targetValue = "(default)";
                                if (i < testMethodArguments.Length)
                                {
                                    targetValue = GetParametersValueData(testMethodArguments[i]);
                                }

                                if (!parameters.Arguments.TryGetValue(methodParameters[i].Name ?? string.Empty, out var argValue))
                                {
                                    matchSignature = false;
                                    break;
                                }

                                if (argValue is not string strArgValue)
                                {
                                    strArgValue = argValue?.ToString() ?? "(null)";
                                }

                                if (strArgValue != targetValue)
                                {
                                    matchSignature = false;
                                    break;
                                }
                            }

                            if (matchSignature)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(currentContext);
            }

            return false;
        }
    }
}
