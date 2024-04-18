// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
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

        internal static void InjectCodeCoverageCollector(ref IEnumerable<string> msbuildArgs)
        {
            const string collectProperty = "-property:VSTestCollect=";
            const string datadogCoverageCollector = "DatadogCoverage";
            const string testAdapterPathProperty = "-property:VSTestTestAdapterPath=";

            var disableTestAdapterInjection = false;
            var codeCoverageCollectorPath = EnvironmentHelpers.GetEnvironmentVariable("DD_CIVISIBILITY_CODE_COVERAGE_COLLECTORPATH") ?? string.Empty;
            if (string.IsNullOrEmpty(codeCoverageCollectorPath))
            {
                Log.Warning("The tracer home directory cannot be found based on the DD_DOTNET_TRACER_HOME value. TestAdapterPath will not be injected.");
                disableTestAdapterInjection = true;
            }

            var isCollectIndex = -1;
            var isTestAdapterPathIndex = -1;
            var msbuildArgsList = msbuildArgs as List<string> ?? [..msbuildArgs];
            for (var i = 0; i < msbuildArgsList.Count; i++)
            {
                if (msbuildArgsList[i].StartsWith(collectProperty))
                {
                    isCollectIndex = i;
                    continue;
                }

                if (msbuildArgsList[i].StartsWith(testAdapterPathProperty))
                {
                    isTestAdapterPathIndex = i;
                }
            }

            if (isCollectIndex == -1)
            {
                // Add the collect property
                Log.Debug("Adding the collect property with the Datadog data collector.");
                msbuildArgsList.Add($"{collectProperty}\"{datadogCoverageCollector}\"");
            }
            else
            {
                // Modify the collect property
                var item = msbuildArgsList[isCollectIndex];
                var values = item.Replace(collectProperty, string.Empty)
                                        .Replace("\"", string.Empty)
                                        .Split(new[] { ';' }, StringSplitOptions.None);

                if (!values.Contains(datadogCoverageCollector))
                {
                    Log.Debug("Appending the Datadog data collector.");
                    item = $"{collectProperty}\"{string.Join(";", values.Concat(datadogCoverageCollector))}\"";
                    msbuildArgsList[isCollectIndex] = item;
                }
                else
                {
                    Log.Debug("Datadog data collector is already in the collector enumerable.");
                }
            }

            if (!disableTestAdapterInjection)
            {
                if (isTestAdapterPathIndex == -1)
                {
                    // Add the test adapter path property
                    Log.Debug("Adding the test adapter path property with the Datadog data collector folder path.");
                    msbuildArgsList.Add($"{testAdapterPathProperty}\"{codeCoverageCollectorPath}\"");
                }
                else
                {
                    // Modify the test adapter path property
                    var item = msbuildArgsList[isTestAdapterPathIndex];
                    var values = item.Replace(testAdapterPathProperty, string.Empty)
                                            .Replace("\"", string.Empty)
                                            .Split(new[] { ';' }, StringSplitOptions.None);

                    if (!values.Contains(codeCoverageCollectorPath))
                    {
                        Log.Debug("Appending the Datadog data collector folder path.");
                        item = $"{testAdapterPathProperty}\"{string.Join(";", values.Concat(codeCoverageCollectorPath))}\"";
                        msbuildArgsList[isTestAdapterPathIndex] = item;
                    }
                    else
                    {
                        Log.Debug("Datadog data collector path is already in the test adapter path enumerable.");
                    }
                }
            }

            // Replace the msbuildArgs with the modified list
            msbuildArgs = msbuildArgsList;
        }
    }
}
