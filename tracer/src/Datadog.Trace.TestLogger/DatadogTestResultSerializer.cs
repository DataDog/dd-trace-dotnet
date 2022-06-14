// <copyright file="DatadogTestResultSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Spekt.TestLogger.Core;

namespace Datadog.Trace.TestLogger;

internal class DatadogTestResultSerializer : ITestResultSerializer
{
    private static int _firstInitialization = 1;

    public string Serialize(LoggerConfiguration loggerConfiguration, TestRunConfiguration runConfiguration, List<TestResultInfo> results, List<TestMessageInfo> messages)
    {
        if (Interlocked.Exchange(ref _firstInitialization, 0) == 1)
        {
            var configurationSource = new CompositeConfigurationSource
            {
                new NameValueConfigurationSource(new NameValueCollection
                {
                    [ConfigurationKeys.CIVisibility.Enabled] = "true",
                    [ConfigurationKeys.CIVisibility.AgentlessEnabled] = "true",
                    [ConfigurationKeys.ApiKey] = GetLoggerApiKey(),
                    [ConfigurationKeys.CIVisibility.Logs] = "true",
                }),
                new EnvironmentConfigurationSource(),
            };
            var ciVisibilitySettings = new CIVisibilitySettings(configurationSource);
            CIVisibility.Initialize(ciVisibilitySettings);
        }

        var framework = FrameworkDescription.Instance;

        string runtimeName = string.Empty;
        string runtimeVersion = string.Empty;

        var runtimeNameAndVersionRegex = new Regex("([a-zA-Z.]*),Version=v([0-9.]*)");
        var match = runtimeNameAndVersionRegex.Match(runConfiguration.TargetFramework);
        if (match.Success && match.Groups.Count == 3)
        {
            runtimeName = match.Groups[1].Value;
            if (runtimeName == ".NETFramework")
            {
                runtimeName = ".NET Framework";
            }
            else if (runtimeName == ".NETCoreApp")
            {
                runtimeName = ".NET";
            }

            runtimeVersion = match.Groups[2].Value;

            if (new Version(runtimeVersion).Major < 4)
            {
                runtimeName = ".NET Core";
            }
        }
        else
        {
            runtimeName = runConfiguration.TargetFramework.IndexOf("NETFramework") != -1 ? ".NET Framework" : ".NET";
            runtimeVersion = runConfiguration.TargetFramework switch
            {
                "NETCoreApp21" => "2.1.0",
                "NETCoreApp30" => "3.0.0",
                "NETCoreApp31" => "3.1.0",
                "NETCoreApp50" => "5.0.0",
                "NETCoreApp60" => "6.0.0",
                "NETFramework461" => "4.6.1",
                _ => runConfiguration.TargetFramework
            };
        }

        foreach (var result in results)
        {
            string testBundle = AssemblyName.GetAssemblyName(result.AssemblyPath).Name!;
            string testSuite = result.FullTypeName;
            string testName = result.Method;

            string? testFramework = null;
            string operationName = "test";
            if (result.TestCase.ExecutorUri.Host.IndexOf("xunit", StringComparison.OrdinalIgnoreCase) != -1)
            {
                testFramework = "xUnit";
                operationName = "xunit.test";
            }
            else if (result.TestCase.ExecutorUri.Host.IndexOf("nunit", StringComparison.OrdinalIgnoreCase) != -1)
            {
                testFramework = "NUnit";
                operationName = "nunit.test";
            }
            else if (result.TestCase.ExecutorUri.Host.IndexOf("mstest", StringComparison.OrdinalIgnoreCase) != -1)
            {
                testFramework = "MSTestV2";
                operationName = "mstest.test";
            }

            Scope scope = Tracer.Instance.StartActiveInternal(operationName, startTime: result.StartTime);
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetTraceSamplingPriority(SamplingPriorityValues.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
            span.SetTag(TestTags.Bundle, testBundle);
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.FrameworkVersion, "N/A");
            span.SetTag(TestTags.Type, TestTags.TypeTest);

            CIEnvironmentValues.Instance.DecorateSpan(span);

            span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
            span.SetTag(CommonTags.RuntimeName, runtimeName);
            span.SetTag(CommonTags.RuntimeVersion, runtimeVersion);
            span.SetTag(CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
            span.SetTag(CommonTags.OSArchitecture, framework.OSArchitecture);
            span.SetTag(CommonTags.OSPlatform, framework.OSPlatform);
            span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

            // Traits
            if (result.Traits.Any())
            {
                var traits = result.Traits.GroupBy(k => k.Name).ToDictionary(k => k.Key, v => v.Select(i => i.Value).ToList());
                span.SetTag(TestTags.Traits, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(traits));
            }

            if (result.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Passed)
            {
                scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
            }
            else if (result.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Skipped)
            {
                scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);

                // xUnit skipped message is stored here
                if (result.Messages.FirstOrDefault(m => m.Category == "StdOutMsgs") is { } xUnitSkipMessage)
                {
                    scope.Span.SetTag(TestTags.SkipReason, xUnitSkipMessage.Text);
                }
                else
                {
                    scope.Span.SetTag(TestTags.SkipReason, result.ErrorMessage);
                }
            }
            else if (result.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.Failed ||
                result.Outcome == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome.NotFound)
            {
                scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                scope.Span.SetTag(Trace.Tags.ErrorMsg, result.ErrorMessage);
                scope.Span.SetTag(Trace.Tags.ErrorStack, result.ErrorStackTrace);
            }

            span.Finish(result.Duration);
            scope.Dispose();
        }

        CIVisibility.FlushSpans();

        return string.Empty;
    }

    private string GetLoggerApiKey()
    {
        var apiKey = Util.EnvironmentHelpers.GetEnvironmentVariable("DD_LOGGER_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = Util.EnvironmentHelpers.GetEnvironmentVariable("DD_API_KEY");
        }

        return apiKey;
    }
}
