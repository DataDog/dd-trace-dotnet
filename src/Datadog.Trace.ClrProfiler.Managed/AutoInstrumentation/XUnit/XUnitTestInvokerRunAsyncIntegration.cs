using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestInvoker`1.RunAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { "xunit.execution.dotnet", "xunit.execution.desktop" },
        Type = "Xunit.Sdk.TestInvoker`1",
        Method = "RunAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1<System.Decimal>",
        ParametersTypesNames = new string[0],
        MinimumVersion = "2.2.0",
        MaximumVersion = "2.*.*",
        IntegrationName = IntegrationName)]
    public static class XUnitTestInvokerRunAsyncIntegration
    {
        private const string IntegrationName = "XUnit";

        static XUnitTestInvokerRunAsyncIntegration()
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
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                return CallTargetState.GetDefault();
            }

            TestInvokerStruct invokerInstance = instance.As<TestInvokerStruct>();

            string testSuite = invokerInstance.TestClass.ToString();
            string testName = invokerInstance.TestMethod.Name;

            AssemblyName testInvokerAssemblyName = instance.GetType().Assembly.GetName();

            Tracer tracer = Tracer.Instance;
            string testFramework = "xUnit " + testInvokerAssemblyName.Version.ToString();

            Scope scope = tracer.StartActive("xunit.test");
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
            object[] testMethodArguments = invokerInstance.TestMethodArguments;
            ParameterInfo[] methodParameters = invokerInstance.TestMethod.GetParameters();
            if (methodParameters?.Length > 0 && testMethodArguments?.Length > 0)
            {
                for (int i = 0; i < methodParameters.Length; i++)
                {
                    if (i < testMethodArguments.Length)
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
            Dictionary<string, List<string>> traits = invokerInstance.TestCase.Traits;
            if (traits.Count > 0)
            {
                foreach (KeyValuePair<string, List<string>> traitValue in traits)
                {
                    span.SetTag($"{TestTags.Traits}.{traitValue.Key}", string.Join(", ", traitValue.Value) ?? "(null)");
                }
            }

            scope.Span.ResetStartTime();
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static decimal OnAsyncMethodEnd<TTarget>(TTarget instance, decimal returnValue, Exception exception, CallTargetState state)
        {
            Scope scope = (Scope)state.State;
            if (scope != null)
            {
                TestInvokerStruct invokerInstance = instance.As<TestInvokerStruct>();
                exception ??= invokerInstance.Aggregator.ToException();

                if (exception != null)
                {
                    if (exception.GetType().Name == "SkipException")
                    {
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusSkip);
                        scope.Span.SetTag(TestTags.SkipReason, exception.Message);
                    }
                    else
                    {
                        scope.Span.SetException(exception);
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                    }
                }
                else
                {
                    scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                }

                scope.Dispose();
            }

            return returnValue;
        }
    }
}
