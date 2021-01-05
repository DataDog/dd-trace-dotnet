using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MsTestV2
{
    /// <summary>
    /// Microsoft.VisualStudio.TestPlatform.TestFramework.Execute calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assembly = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter",
        Type = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodRunner",
        Method = "Execute",
        ReturnTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestResult",
        ParametersTypesNames = new string[0],
        MinimumVersion = "14.0.0",
        MaximumVersion = "14.*.*",
        IntegrationName = IntegrationName)]
    public class TestMethodRunnerExecuteIntegration
    {
        private const string IntegrationName = "MSTestV2";
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(TestMethodRunnerExecuteIntegration));

        static TestMethodRunnerExecuteIntegration()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : ITestMethodRunner, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                return CallTargetState.GetDefault();
            }

            ITestMethod testMethodInfo = instance.TestMethodInfo;
            MethodInfo testMethod = testMethodInfo.MethodInfo;
            object[] testMethodArguments = testMethodInfo.Arguments;

            string testFramework = "MSTestV2 " + instance.Type.Assembly.GetName().Version;
            string testSuite = testMethodInfo.TestClassName;
            string testName = testMethodInfo.TestMethodName;

            Tracer tracer = Tracer.Instance;
            Scope scope = tracer.StartActive("mstest.test");
            Span span = scope.Span;

            span.Type = SpanTypes.Test;
            span.SetMetric(Tags.Analytics, 1.0d);
            span.SetTraceSamplingPriority(SamplingPriority.AutoKeep);
            span.ResourceName = $"{testSuite}.{testName}";
            span.SetTag(TestTags.Suite, testSuite);
            span.SetTag(TestTags.Name, testName);
            span.SetTag(TestTags.Framework, testFramework);
            span.SetTag(TestTags.Type, TestTags.TypeTest);
            CIEnvironmentValues.DecorateSpan(span);

            var framework = FrameworkDescription.Instance;

            span.SetTag(CommonTags.RuntimeName, framework.Name);
            span.SetTag(CommonTags.RuntimeOSArchitecture, framework.OSArchitecture);
            span.SetTag(CommonTags.RuntimeOSPlatform, framework.OSPlatform);
            span.SetTag(CommonTags.RuntimeProcessArchitecture, framework.ProcessArchitecture);
            span.SetTag(CommonTags.RuntimeVersion, framework.ProductVersion);

            // Get test parameters
            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (testMethodArguments != null && i < testMethodArguments.Length)
                    {
                        span.SetTag($"{TestTags.Arguments}.{methodParameters[i].Name}", testMethodArguments[i]?.ToString() ?? "(null)");
                    }
                    else
                    {
                        span.SetTag($"{TestTags.Arguments}.{methodParameters[i].Name}", "(default)");
                    }
                }
            }

            // Get traits
            Dictionary<string, List<string>> testTraits = GetTraits(testMethod);
            if (testTraits != null)
            {
                foreach (KeyValuePair<string, List<string>> keyValuePair in testTraits)
                {
                    span.SetTag($"{TestTags.Traits}.{keyValuePair.Key}", string.Join(", ", keyValuePair.Value) ?? "(null)");
                }
            }

            span.ResetStartTime();
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : ITestMethodRunner
        {
            Scope scope = (Scope)state.State;
            if (scope != null)
            {
                Array returnValueArray = returnValue as Array;
                if (returnValueArray.Length == 1)
                {
                    object unitTestResultObject = returnValueArray.GetValue(0);
                    if (unitTestResultObject != null)
                    {
                        UnitTestResultStruct unitTestResult = unitTestResultObject.As<UnitTestResultStruct>();

                        switch (unitTestResult.Outcome)
                        {
                            case UnitTestResultOutcome.Error:
                            case UnitTestResultOutcome.Failed:
                            case UnitTestResultOutcome.NotFound:
                            case UnitTestResultOutcome.Timeout:
                                scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                                scope.Span.Error = true;
                                scope.Span.SetTag(Tags.ErrorMsg, unitTestResult.ErrorMessage);
                                scope.Span.SetTag(Tags.ErrorStack, unitTestResult.ErrorStackTrace);
                                break;
                            case UnitTestResultOutcome.Inconclusive:
                            case UnitTestResultOutcome.NotRunnable:
                            case UnitTestResultOutcome.Ignored:
                                scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                                scope.Span.SetTag(TestTags.SkipReason, unitTestResult.ErrorMessage);
                                break;
                            case UnitTestResultOutcome.Passed:
                                scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                                break;
                        }
                    }
                }

                scope.Dispose();
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        private static Dictionary<string, List<string>> GetTraits(MethodInfo methodInfo)
        {
            Dictionary<string, List<string>> testProperties = null;
            var testAttributes = methodInfo.GetCustomAttributes(true);
            foreach (var tattr in testAttributes)
            {
                if (tattr?.GetType().Name != "TestCategoryAttribute")
                {
                    continue;
                }

                testProperties ??= new Dictionary<string, List<string>>();
                if (!testProperties.TryGetValue("Category", out var categoryList))
                {
                    categoryList = new List<string>();
                    testProperties["Category"] = categoryList;
                }

                categoryList.AddRange(tattr.As<TestCategoryAttributeStruct>().TestCategories);
            }

            var classCategories = methodInfo.DeclaringType?.GetCustomAttributes(true);
            if (!(classCategories is null))
            {
                foreach (var tattr in classCategories)
                {
                    if (tattr.GetType().Name != "TestCategoryAttribute")
                    {
                        continue;
                    }

                    testProperties ??= new Dictionary<string, List<string>>();
                    if (!testProperties.TryGetValue("Category", out var categoryList))
                    {
                        categoryList = new List<string>();
                        testProperties["Category"] = categoryList;
                    }

                    categoryList.AddRange(tattr.As<TestCategoryAttributeStruct>().TestCategories);
                }
            }

            return testProperties;
        }
    }
}
