// <copyright file="NUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    internal static class NUnitIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.NUnit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.NUnit;
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));

        internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

        internal static Scope CreateScope(ITest currentTest, Type targetType)
        {
            MethodInfo testMethod = currentTest.Method.MethodInfo;
            object[] testMethodArguments = currentTest.Arguments;
            IPropertyBag testMethodProperties = currentTest.Properties;

            if (testMethod == null)
            {
                return null;
            }

            string testFramework = "NUnit";
            string fullName = currentTest.FullName;
            string composedTestName = currentTest.Name;

            string testName = testMethod.Name;
            string testSuite = testMethod.DeclaringType?.FullName;
            string testBundle = testMethod.DeclaringType?.Assembly?.GetName().Name;

            // Extract the test suite from the full name to support custom fixture parameters and test declared in base classes.
            if (fullName.EndsWith("." + composedTestName))
            {
                testSuite = fullName.Substring(0, fullName.Length - (composedTestName.Length + 1));
            }

            string skipReason = null;

            Scope scope = Tracer.Instance.StartActiveInternal("nunit.test");
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetTraceSamplingDecision(SamplingPriorityValues.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(Tags.Origin, TestTags.CIAppTestOriginName);
            span.SetTag(TestTags.Bundle, testBundle);
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.FrameworkVersion, targetType.Assembly?.GetName().Version.ToString());
            span.SetTag(TestTags.Type, TestTags.TypeTest);
            CIEnvironmentValues.Instance.DecorateSpan(span);

            var framework = FrameworkDescription.Instance;
            span.SetTag(CommonTags.LibraryVersion, TracerConstants.AssemblyVersion);
            span.SetTag(CommonTags.RuntimeName, framework.Name);
            span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);
            span.SetTag(CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
            span.SetTag(CommonTags.OSArchitecture, framework.OSArchitecture);
            span.SetTag(CommonTags.OSPlatform, framework.OSPlatform);
            span.SetTag(CommonTags.OSVersion, Environment.OSVersion.VersionString);

            // Get test parameters
            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                TestParameters testParameters = new TestParameters();
                testParameters.Metadata = new Dictionary<string, object>();
                testParameters.Arguments = new Dictionary<string, object>();
                testParameters.Metadata[TestTags.MetadataTestName] = currentTest.Name;

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (testMethodArguments != null && i < testMethodArguments.Length)
                    {
                        testParameters.Arguments[methodParameters[i].Name] = Common.GetParametersValueData(testMethodArguments[i]);
                    }
                    else
                    {
                        testParameters.Arguments[methodParameters[i].Name] = "(default)";
                    }
                }

                span.SetTag(TestTags.Parameters, testParameters.ToJSON());
            }

            // Get traits
            if (testMethodProperties != null)
            {
                Dictionary<string, List<string>> traits = new Dictionary<string, List<string>>();
                skipReason = (string)testMethodProperties.Get("_SKIPREASON");
                foreach (var key in testMethodProperties.Keys)
                {
                    if (key == "_SKIPREASON" || key == "_JOINTYPE")
                    {
                        continue;
                    }

                    IList value = testMethodProperties[key];
                    if (value != null)
                    {
                        List<string> lstValues = new List<string>();
                        foreach (object valObj in value)
                        {
                            if (valObj is null)
                            {
                                continue;
                            }

                            lstValues.Add(valObj.ToString());
                        }

                        traits[key] = lstValues;
                    }
                    else
                    {
                        traits[key] = null;
                    }
                }

                if (traits.Count > 0)
                {
                    span.SetTag(TestTags.Traits, Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(traits));
                }
            }

            // Test code and code owners
            Common.DecorateSpanWithSourceAndCodeOwners(span, testMethod);

            Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            Common.StartCoverage();

            if (skipReason != null)
            {
                FinishSkippedScope(scope, skipReason);
                scope = null;
            }

            span.ResetStartTime();
            return scope;
        }

        internal static void FinishScope(Scope scope, Exception ex)
        {
            try
            {
                // unwrap the generic NUnitException
                if (ex != null && ex.GetType().FullName == "NUnit.Framework.Internal.NUnitException")
                {
                    ex = ex.InnerException;
                }

                if (ex != null)
                {
                    string exTypeName = ex.GetType().FullName;

                    if (exTypeName == "NUnit.Framework.SuccessException")
                    {
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                        scope.Span.SetTag(TestTags.Message, ex.Message);
                    }
                    else if (exTypeName is "NUnit.Framework.IgnoreException" or "NUnit.Framework.InconclusiveException")
                    {
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                        scope.Span.SetTag(TestTags.SkipReason, ex.Message);
                    }
                    else
                    {
                        scope.Span.SetException(ex);
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                    }
                }
                else
                {
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                }
            }
            finally
            {
                scope.Dispose();
                Common.StopCoverage(scope.Span);
            }
        }

        internal static void FinishSkippedScope(Scope scope, string skipReason)
        {
            var span = scope?.Span;
            if (span != null)
            {
                try
                {
                    span.SetTag(TestTags.Status, TestTags.StatusSkip);
                    span.SetTag(TestTags.SkipReason, skipReason ?? string.Empty);
                    span.Finish(TimeSpan.Zero);
                }
                finally
                {
                    scope.Dispose();
                    Common.StopCoverage(span);
                }
            }
        }
    }
}
