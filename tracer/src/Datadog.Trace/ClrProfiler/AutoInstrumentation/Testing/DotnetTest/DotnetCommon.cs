// <copyright file="DotnetCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest
{
    internal static class DotnetCommon
    {
        internal const string DotnetTestIntegrationName = nameof(IntegrationId.DotnetTest);
        internal const IntegrationId DotnetTestIntegrationId = Configuration.IntegrationId.DotnetTest;

        internal static readonly IDatadogLogger Log = Ci.CIVisibility.Log;
        private static bool? _isDataCollectorDomainCache;

        internal static bool DotnetTestIntegrationEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(DotnetTestIntegrationId);

        internal static bool IsDataCollectorDomain
        {
            get
            {
                if (_isDataCollectorDomainCache is { } value)
                {
                    return value;
                }

                return (_isDataCollectorDomainCache = DomainMetadata.Instance.AppDomainName.ToLowerInvariant().Contains("datacollector")).Value;
            }
        }

        internal static TestSession? CreateSession()
        {
            // We create test session if not DataCollector
            if (IsDataCollectorDomain)
            {
                return null;
            }

            // Let's detect if we already have a session for this test process
            if (SpanContextPropagator.Instance.Extract(
                    EnvironmentHelpers.GetEnvironmentVariables(),
                    new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor)) is not null)
            {
                // Session found in the environment variables
                // let's bail-out
                return null;
            }

            var ciVisibilitySettings = CIVisibility.Settings;
            var agentless = ciVisibilitySettings.Agentless;
            var isEvpProxy = CIVisibility.EventPlatformProxySupport != EventPlatformProxySupport.None;

            Log.Information("CreateSession: Agentless: {Agentless} | IsEvpProxy: {IsEvpProxy}", agentless, isEvpProxy);

            // We create a test session if the flag is turned on (agentless or evp proxy)
            if (agentless || isEvpProxy)
            {
                // Try to load the command line propagated from the dd-trace ci run command
                if (EnvironmentHelpers.GetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable) is not { Length: > 0 } commandLine)
                {
                    commandLine = Environment.CommandLine;
                }

                // Try to load the working directory propagated from the dd-trace ci run command
                if (EnvironmentHelpers.GetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable) is not { Length: > 0 } workingDirectory)
                {
                    workingDirectory = Environment.CurrentDirectory;
                }

                var session = TestSession.InternalGetOrCreate(commandLine, workingDirectory, null, null, true);
                session.SetTag(IntelligentTestRunnerTags.TestTestsSkippingEnabled, ciVisibilitySettings.TestsSkippingEnabled == true ? "true" : "false");
                session.SetTag(CodeCoverageTags.Enabled, ciVisibilitySettings.CodeCoverageEnabled == true ? "true" : "false");
                if (ciVisibilitySettings.EarlyFlakeDetectionEnabled == true)
                {
                    session.SetTag(EarlyFlakeDetectionTags.Enabled, "true");
                }

                // At session level we know if the ITR is disabled (meaning that no tests will be skipped)
                // In that case we tell the backend no tests are going to be skipped.
                if (ciVisibilitySettings.TestsSkippingEnabled == false)
                {
                    session.SetTag(IntelligentTestRunnerTags.TestsSkipped, "false");
                }

                // We enable the IPC server for the session
                session.EnableIpcServer();

                return session;
            }

            Log.Information("CreateSession: Session was not created.");
            return null;
        }

        internal static void FinalizeSession(TestSession? session, int exitCode, Exception? exception)
        {
            if (session is null)
            {
                return;
            }

            session.SetTag(TestTags.CommandExitCode, exitCode);

            if (exception is not null)
            {
                session.SetErrorInfo(exception);
            }

            var codeCoveragePath = EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoveragePath);

            // If the code coverage path is set we try to read all json files created, merge them into a single one and extract the
            // global code coverage percentage.
            // Note: we also write the total global code coverage to the `session-coverage-{date}.json` file
            if (!string.IsNullOrEmpty(codeCoveragePath))
            {
                if (!string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath)))
                {
                    Log.Warning("DD_CIVISIBILITY_EXTERNAL_CODE_COVERAGE_PATH was ignored because DD_CIVISIBILITY_CODE_COVERAGE_ENABLED is set.");
                }

                var outputPath = Path.Combine(codeCoveragePath, $"session-coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}.json");
                if (CoverageUtils.TryCombineAndGetTotalCoverage(codeCoveragePath, outputPath, out var globalCoverage) &&
                    globalCoverage is not null)
                {
                    // We only report the code coverage percentage if the customer manually sets the 'DD_CIVISIBILITY_CODE_COVERAGE_ENABLED' environment variable according to the new spec.
                    if (EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.CodeCoverage)?.ToBoolean() == true)
                    {
                        // Adds the global code coverage percentage to the session
                        session.SetTag(CodeCoverageTags.PercentageOfTotalLines, globalCoverage.GetTotalPercentage());
                    }
                }
            }
            else
            {
                try
                {
                    if (EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.CIVisibility.ExternalCodeCoveragePath) is { Length: > 0 } extCodeCoverageFilePath &&
                        File.Exists(extCodeCoverageFilePath))
                    {
                        if (TryGetCoveragePercentageFromXml(extCodeCoverageFilePath, out var coveragePercentage))
                        {
                            session.SetTag(CodeCoverageTags.PercentageOfTotalLines, coveragePercentage);
                        }
                        else
                        {
                            Log.Warning("RunCiCommand: Error while reading the external file code coverage. Format is not supported.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "RunCiCommand: Error while reading the external file code coverage.");
                }
            }

            session.Close(exitCode == 0 ? TestStatus.Pass : TestStatus.Fail);
        }

        internal static bool TryGetCoveragePercentageFromXml(string filePath, out double percentage)
        {
            percentage = 0;
            if (!File.Exists(filePath))
            {
                return false;
            }

            // Load Code Coverage from the file.
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            if (xmlDoc.SelectSingleNode("/CoverageSession/Summary/@sequenceCoverage") is { } seqCovAttribute &&
                double.TryParse(seqCovAttribute.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seqCovValue))
            {
                // Found using the OpenCover format.
                percentage = Math.Round(seqCovValue, 2).ToValidPercentage();
                Log.Debug("TryGetCoveragePercentageFromXml: OpenCover code coverage was reported: {Value}", seqCovValue);
                return true;
            }

            if (xmlDoc.SelectSingleNode("/coverage/@line-rate") is { } lineRateAttribute &&
                double.TryParse(lineRateAttribute.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lineRateValue))
            {
                // Found using the Cobertura format.
                percentage = Math.Round(lineRateValue * 100, 2).ToValidPercentage();
                Log.Debug("TryGetCoveragePercentageFromXml: Cobertura code coverage was reported: {Value}", lineRateAttribute.Value);
                return true;
            }

            var linesCovered = xmlDoc.SelectNodes("/results/modules/module/@lines_covered");
            var linesPartiallyCovered = xmlDoc.SelectNodes("/results/modules/module/@lines_partially_covered");
            var linesNotCovered = xmlDoc.SelectNodes("/results/modules/module/@lines_not_covered");

            if (linesCovered != null && linesPartiallyCovered != null && linesNotCovered != null &&
                linesCovered.Count == linesPartiallyCovered.Count && linesCovered.Count == linesNotCovered.Count)
            {
                // Found using Microsoft.CodeCoverage xml format
                var modulesCount = linesCovered.Count;

                var totalLinesCovered = 0d;
                foreach (XmlNode? lineCovered in linesCovered)
                {
                    if (lineCovered is null)
                    {
                        continue;
                    }

                    if (double.TryParse(lineCovered.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                    {
                        totalLinesCovered += value;
                    }
                    else
                    {
                        return false;
                    }
                }

                var totalLinesPartiallyCovered = 0d;
                foreach (XmlNode? linePartiallyCovered in linesPartiallyCovered)
                {
                    if (linePartiallyCovered is null)
                    {
                        continue;
                    }

                    if (double.TryParse(linePartiallyCovered.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                    {
                        totalLinesPartiallyCovered += value;
                    }
                    else
                    {
                        return false;
                    }
                }

                var totalLinesNotCovered = 0d;
                foreach (XmlNode? lineNotCovered in linesNotCovered)
                {
                    if (lineNotCovered is null)
                    {
                        continue;
                    }

                    if (double.TryParse(lineNotCovered.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                    {
                        totalLinesNotCovered += value;
                    }
                    else
                    {
                        return false;
                    }
                }

                var totalLines = totalLinesCovered + totalLinesPartiallyCovered + totalLinesNotCovered;
                percentage = Math.Round((totalLinesCovered / totalLines) * 100, 2).ToValidPercentage();
                Log.Debug("TryGetCoveragePercentageFromXml: Microsoft.CodeCoverage code coverage was reported: {Value}", percentage);
                return true;
            }

            return false;
        }

        internal static void InjectCodeCoverageCollectorToDotnetTest(ref IEnumerable<string>? msbuildArgs)
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
                Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing raw collect property values: {CollectProperty}", item);
                var cleanItem = item.Replace(collectProperty, string.Empty)
                                    .Replace("\"", string.Empty);
                Log.Debug("InjectCodeCoverageCollector.DotnetTest: Existing clean collect property values: {CollectProperty}", cleanItem);
                var values = cleanItem.Split(new[] { ';' }, StringSplitOptions.None);

                if (!values.Contains(datadogCoverageCollector, StringComparer.OrdinalIgnoreCase))
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

        internal static void WriteDebugInfoForDotnetTest(IEnumerable<string>? msbuildArgs, IEnumerable<string>? userDefinedArguments, IEnumerable<string>? trailingArguments, bool noRestore, string? msbuildPath)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                sb.AppendLine("InjectCodeCoverageCollector.DotnetTest: Microsoft.DotNet.Tools.Test.TestCommand..ctor arguments:");

                if (msbuildArgs is not null)
                {
                    sb.AppendLine("\tmsbuildArgs: ");
                    foreach (var arg in msbuildArgs)
                    {
                        sb.AppendLine($"\t\t{arg}");
                    }
                }

                if (userDefinedArguments is not null)
                {
                    sb.AppendLine("\tuserDefinedArguments: ");
                    foreach (var arg in userDefinedArguments)
                    {
                        sb.AppendLine($"\t\t{arg}");
                    }
                }

                if (trailingArguments is not null)
                {
                    sb.AppendLine("\ttrailingArguments: ");
                    foreach (var arg in trailingArguments)
                    {
                        sb.AppendLine($"\t\t{arg}");
                    }
                }

                sb.AppendLine("\tnoRestore: " + noRestore);
                sb.AppendLine("\tmsbuildPath: " + msbuildPath);
                Log.Debug("{MessageValue}", StringBuilderCache.GetStringAndRelease(sb));
            }
        }

        internal static void InjectCodeCoverageCollectorToVsConsoleTest(ref string[]? args)
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

        internal static void WriteDebugInfoForVsConsoleTest(string[]? args)
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
