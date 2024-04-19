// <copyright file="Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal const string DotnetTestIntegrationName = nameof(IntegrationId.DotnetTest);
        internal const IntegrationId DotnetTestIntegrationId = Configuration.IntegrationId.DotnetTest;

        internal static readonly IDatadogLogger Log = Ci.CIVisibility.Log;

        internal static bool DotnetTestIntegrationEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(DotnetTestIntegrationId);

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

        internal static void InjectCodeCoverageCollectorToDotnetTest(ref IEnumerable<string> msbuildArgs)
        {
            if (msbuildArgs == null)
            {
                return;
            }

            // In this case a single property contains multiple values separated by a semi colon
            const string collectProperty = "-property:VSTestCollect=";
            const string datadogCoverageCollector = "DatadogCoverage";
            const string testAdapterPathProperty = "-property:VSTestTestAdapterPath=";

            var disableTestAdapterInjection = false;
            var codeCoverageCollectorPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath) ?? string.Empty;
            if (string.IsNullOrEmpty(codeCoverageCollectorPath))
            {
                Log.Warning("InjectCodeCoverageCollector.DotnetTest: The tracer home directory cannot be found based on the DD_CIVISIBILITY_CODE_COVERAGE_COLLECTORPATH value. TestAdapterPath will not be injected.");
                disableTestAdapterInjection = true;
            }

            var isCollectIndex = -1;
            var isTestAdapterPathIndex = -1;
            var msbuildArgsList = msbuildArgs as List<string> ?? [..msbuildArgs];
            for (var i = 0; i < msbuildArgsList.Count; i++)
            {
                var arg = msbuildArgsList[i];
                if (arg.StartsWith(collectProperty, StringComparison.OrdinalIgnoreCase))
                {
                    isCollectIndex = i;
                    continue;
                }

                if (arg.StartsWith(testAdapterPathProperty, StringComparison.OrdinalIgnoreCase))
                {
                    isTestAdapterPathIndex = i;
                }
            }

            if (isCollectIndex == -1)
            {
                // Add the collect property
                Log.Information("InjectCodeCoverageCollector.DotnetTest: Adding the collect property with the Datadog data collector.");
                msbuildArgsList.Add($"{collectProperty}\"{datadogCoverageCollector}\"");
            }
            else
            {
                // Modify the collect property
                var item = msbuildArgsList[isCollectIndex];
                var cleanItem = item.Replace(collectProperty, string.Empty)
                                    .Replace("\"", string.Empty);
                Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing collect property values: {CollectProperty}", cleanItem);
                var values = cleanItem.Split(new[] { ';' }, StringSplitOptions.None);

                if (!values.Contains(datadogCoverageCollector))
                {
                    Log.Information("InjectCodeCoverageCollector.DotnetTest: Appending the Datadog data collector.");
                    item = $"{collectProperty}\"{string.Join(";", values.Concat(datadogCoverageCollector))}\"";
                    msbuildArgsList[isCollectIndex] = item;
                }
                else
                {
                    Log.Information("InjectCodeCoverageCollector.DotnetTest: Datadog data collector is already in the collector enumerable.");
                }
            }

            if (!disableTestAdapterInjection)
            {
                if (isTestAdapterPathIndex == -1)
                {
                    // Add the test adapter path property
                    Log.Information("InjectCodeCoverageCollector.DotnetTest: Adding the test adapter path property with the Datadog data collector folder path.");
                    msbuildArgsList.Add($"{testAdapterPathProperty}\"{codeCoverageCollectorPath}\"");
                }
                else
                {
                    // Modify the test adapter path property
                    var item = msbuildArgsList[isTestAdapterPathIndex];
                    var cleanItem = item.Replace(testAdapterPathProperty, string.Empty)
                                        .Replace("\"", string.Empty);
                    Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing testAdapter property values: {CollectProperty}", cleanItem);
                    var values = cleanItem.Split(new[] { ';' }, StringSplitOptions.None);

                    if (!values.Contains(codeCoverageCollectorPath))
                    {
                        Log.Information("InjectCodeCoverageCollector.DotnetTest: Appending the Datadog data collector folder path.");
                        item = $"{testAdapterPathProperty}\"{string.Join(";", values.Concat(codeCoverageCollectorPath))}\"";
                        msbuildArgsList[isTestAdapterPathIndex] = item;
                    }
                    else
                    {
                        Log.Information("InjectCodeCoverageCollector.DotnetTest: Datadog data collector path is already in the test adapter path enumerable.");
                    }
                }
            }

            // Replace the msbuildArgs with the modified list
            msbuildArgs = msbuildArgsList;
        }

        internal static void WriteDebugInfoForDotnetTest(IEnumerable<string> msbuildArgs, IEnumerable<string> userDefinedArguments, IEnumerable<string> trailingArguments, bool noRestore, string msbuildPath)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                sb.AppendLine("InjectCodeCoverageCollector.DotnetTest: Microsoft.DotNet.Tools.Test.TestCommand..ctor arguments:");

                if (msbuildArgs is not null)
                {
                    sb.AppendLine("\tmsbuildArgs: ");
                    if (msbuildArgs is not null)
                    {
                        foreach (var arg in msbuildArgs)
                        {
                            sb.AppendLine($"\t\t{arg}");
                        }
                    }
                }

                if (userDefinedArguments is not null)
                {
                    sb.AppendLine("\tuserDefinedArguments: ");
                    if (userDefinedArguments is not null)
                    {
                        foreach (var arg in userDefinedArguments)
                        {
                            sb.AppendLine($"\t\t{arg}");
                        }
                    }
                }

                if (trailingArguments is not null)
                {
                    sb.AppendLine("\ttrailingArguments: ");
                    if (trailingArguments is not null)
                    {
                        foreach (var arg in trailingArguments)
                        {
                            sb.AppendLine($"\t\t{arg}");
                        }
                    }
                }

                sb.AppendLine("\tnoRestore: " + noRestore);
                sb.AppendLine("\tmsbuildPath: " + msbuildPath);
                Log.Debug("{MessageValue}", StringBuilderCache.GetStringAndRelease(sb));
            }
        }

        internal static void InjectCodeCoverageCollectorToVsConsoleTest(ref string[] args)
        {
            if (args == null)
            {
                return;
            }

            // In this case each property contains just a single value, for multiple values we need to add multiple arguments
            const string collectProperty = "/Collect:";
            const string datadogCoverageCollector = "DatadogCoverage";
            const string testAdapterPathProperty = "/TestAdapterPath:";

            var disableCollectInjection = false;
            var disableTestAdapterInjection = false;
            var codeCoverageCollectorPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.CIVisibility.CodeCoverageCollectorPath) ?? string.Empty;
            if (string.IsNullOrEmpty(codeCoverageCollectorPath))
            {
                Log.Warning("InjectCodeCoverageCollector.VsConsoleTest:: The tracer home directory cannot be found based on the DD_CIVISIBILITY_CODE_COVERAGE_COLLECTORPATH value. TestAdapterPath will not be injected.");
                disableTestAdapterInjection = true;
            }

            var lstArgs = new List<string>(args);

            UpdateIndexes(lstArgs, codeCoverageCollectorPath, out var isCollectIndex, out var isTestAdapterPathIndex, out var doubleDashIndex, ref disableCollectInjection, ref disableTestAdapterInjection);
            if (disableCollectInjection && disableTestAdapterInjection)
            {
                // Nothing to modify
                return;
            }

            if (!disableTestAdapterInjection)
            {
                Inject(lstArgs, $"{testAdapterPathProperty}\"{codeCoverageCollectorPath}\"", doubleDashIndex, isTestAdapterPathIndex);
                UpdateIndexes(lstArgs, codeCoverageCollectorPath, out isCollectIndex, out isTestAdapterPathIndex, out doubleDashIndex, ref disableCollectInjection, ref disableTestAdapterInjection);
            }

            if (!disableCollectInjection)
            {
                Inject(lstArgs, $"{collectProperty}{datadogCoverageCollector}", doubleDashIndex, isCollectIndex);
            }

            args = lstArgs.ToArray();

            static void UpdateIndexes(List<string> lstArgs, string codeCoverageCollectorPath, out int isCollectIndex, out int isTestAdapterPathIndex, out int doubleDashIndex, ref bool disableCollectInjection, ref bool disableTestAdapterInjection)
            {
                isCollectIndex = -1;
                isTestAdapterPathIndex = -1;
                doubleDashIndex = int.MaxValue;
                for (var i = 0; i < lstArgs.Count; i++)
                {
                    var arg = lstArgs[i];
                    if (arg == "--" && doubleDashIndex > i)
                    {
                        doubleDashIndex = i;
                        continue;
                    }

                    if (!disableCollectInjection && arg.StartsWith(collectProperty, StringComparison.OrdinalIgnoreCase))
                    {
                        isCollectIndex = i;
                        var argValue = arg.Replace(collectProperty, string.Empty)
                                                .Replace("\"", string.Empty);
                        disableCollectInjection = disableCollectInjection || argValue == datadogCoverageCollector;
                        continue;
                    }

                    if (!disableTestAdapterInjection && arg.StartsWith(testAdapterPathProperty, StringComparison.OrdinalIgnoreCase))
                    {
                        isTestAdapterPathIndex = i;
                        var argValue = arg.Replace(testAdapterPathProperty, string.Empty)
                                                .Replace("\"", string.Empty);
                        disableTestAdapterInjection = disableTestAdapterInjection || argValue == codeCoverageCollectorPath;
                    }
                }
            }

            static void Inject(List<string> lstArgs, string value, int doubleDashIndex, int propertyIndex)
            {
                Log.Debug<string, int, int>("InjectCodeCoverageCollector.VsConsoleTest: [ Value={Value} | DoubleDash={DoubleDash} | PropertyIndex={PropertyIndex} ]", value, doubleDashIndex, propertyIndex);
                if (propertyIndex != -1 && propertyIndex < doubleDashIndex)
                {
                    // If there's already an existing property index we insert it just after that one.
                    Log.Information("InjectCodeCoverageCollector.VsConsoleTest: There's already an existing property index we insert it just after that one.");
                    lstArgs.Insert(propertyIndex + 1, value);
                }
                else if (doubleDashIndex < lstArgs.Count)
                {
                    // If we found an arg with "--" we insert it before that arg (everything after is considered as test settings).
                    Log.Information("InjectCodeCoverageCollector.VsConsoleTest: We found an arg with \"--\" we insert it before that arg (everything after is considered as test settings).");
                    lstArgs.Insert(doubleDashIndex, value);
                }
                else
                {
                    // If we don't have neither the "--" nor any property index, we just append the property to the current arg list.
                    Log.Information("InjectCodeCoverageCollector.VsConsoleTest: We don't have neither the \"--\" nor a property index, we just append the property to the current arg list.");
                    lstArgs.Add(value);
                }
            }
        }

        internal static void WriteDebugInfoForVsConsoleTest(string[] args)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                sb.AppendLine("InjectCodeCoverageCollector.VsConsoleTest: arguments:");

                if (args != null)
                {
                    for (var i = 0; i < args.Length; i++)
                    {
                        sb.AppendLine($"\t{i}:{args[i]}");
                    }
                }

                Log.Debug("{MessageValue}", StringBuilderCache.GetStringAndRelease(sb));
            }
        }
    }
}
