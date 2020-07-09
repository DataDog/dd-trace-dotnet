using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// Tracing integration for XUnit testing framework
    /// </summary>
    public static class XUnitIntegration
    {
        private const string IntegrationName = "XUnit";

        private const string XUnitAssembly = "xunit.execution.dotnet";

        private const string XUnitTestInvokerType = "Xunit.Sdk.TestInvoker`1";
        private const string XUnitCallTestMethod = "CallTestMethod";

        private const string XUnitTestRunnerType = "Xunit.Sdk.TestRunner`1";
        private const string XUnitRunAsyncMethod = "RunAsync";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(XUnitIntegration));

        private static long _testInvokerScopesCount;
        private static long _testInvokerReturnTask;
        private static long _testInvokerScopesDisposedCount;
        private static long _testRunAsyncSkipped;

        /// <summary>
        /// Wrap the original Xunit.Sdk.TestInvoker`1.CallTestMethod method by adding instrumentation code around it.
        /// </summary>
        /// <param name="testInvoker">The TestInvoker instance we are replacing.</param>
        /// <param name="testClassInstance">The TestClass instance.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = XUnitAssembly,
            TargetType = XUnitTestInvokerType,
            TargetMethod = XUnitCallTestMethod,
            TargetSignatureTypes = new[] { ClrNames.Object, ClrNames.Object })]
        public static object CallTestMethod(
            object testInvoker,
            object testClassInstance,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testInvoker == null) { throw new ArgumentNullException(nameof(testInvoker)); }

            Type testInvokerType = testInvoker.GetType();
            Func<object, object, object> execute;

            try
            {
                execute =
                    MethodBuilder<Func<object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, XUnitCallTestMethod)
                       .WithConcreteType(testInvokerType)
                       .WithParameters(testClassInstance)
                       .WithDeclaringTypeGenerics(testInvokerType.BaseType.GenericTypeArguments)
                       .WithNamespaceAndNameFilters(ClrNames.Object, ClrNames.Object)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: XUnitTestInvokerType,
                    methodName: XUnitCallTestMethod,
                    instanceType: testInvokerType.AssemblyQualifiedName);
                throw;
            }

            var scope = CreateScope(testInvoker, testClassInstance.GetType());
            if (scope is null)
            {
                Log.Debug($"** SCOPE is null!!!");
                return execute(testInvoker, testClassInstance);
            }

            Log.Debug($"** SCOPE Created for TestInvoker. [{Interlocked.Increment(ref _testInvokerScopesCount)}:{Interlocked.Read(ref _testInvokerScopesDisposedCount)}:{Interlocked.Read(ref _testInvokerReturnTask)}]");

            object returnValue = null;
            Exception exception = null;
            try
            {
                returnValue = execute(testInvoker, testClassInstance);
            }
            catch (TargetInvocationException ex)
            {
                exception = ex.InnerException;
                throw;
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                returnValue = AsyncTool.AddContinuation(returnValue, exception, (r, ex) =>
                {
                    if (ex != null)
                    {
                        scope.Span.SetException(ex);
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                    }
                    else
                    {
                        scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
                    }

                    scope.Dispose();
                    Log.Debug($"** SCOPE Disposed for TestInvoker. [{Interlocked.Read(ref _testInvokerScopesCount)}:{Interlocked.Increment(ref _testInvokerScopesDisposedCount)}:{Interlocked.Read(ref _testInvokerReturnTask)}]");
                    return r;
                });
            }

            return returnValue;
        }

        /// <summary>
        /// Wrap the original Xunit.Sdk.TestRunner`1.RunAsync method by adding instrumentation code around it
        /// </summary>
        /// <param name="testRunner">The TestRunner instance we are replacing.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = XUnitAssembly,
            TargetType = XUnitTestRunnerType,
            TargetMethod = XUnitRunAsyncMethod,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>" })]
        public static object RunAsync(
            object testRunner,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testRunner == null) { throw new ArgumentNullException(nameof(testRunner)); }

            Type testRunnerType = testRunner.GetType();
            Func<object, object> execute;

            try
            {
                execute =
                    MethodBuilder<Func<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, XUnitRunAsyncMethod)
                       .WithConcreteType(testRunnerType)
                       .WithDeclaringTypeGenerics(testRunnerType.BaseType.GenericTypeArguments)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: XUnitTestRunnerType,
                    methodName: XUnitRunAsyncMethod,
                    instanceType: testRunnerType.AssemblyQualifiedName);
                throw;
            }

            if (!(CreateScope(testRunner, null) is null))
            {
                Log.Debug($"** SCOPE Test SKIPPED. [{Interlocked.Increment(ref _testRunAsyncSkipped)}]");
            }

            return execute(testRunner);
        }

        private static Scope CreateScope(object testSdk, Type testClassType)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string testSuite = null;
            string testName = null;
            string skipReason = null;
            List<KeyValuePair<string, string>> testArguments = null;

            if (testClassType is null)
            {
                if (!testSdk.TryGetPropertyValue<Type>("TestClass", out testClassType))
                {
                    Log.TestClassTypeNotFound();
                    return null;
                }

                testSdk.TryGetPropertyValue<string>("SkipReason", out skipReason);
                if (skipReason == null)
                {
                    return null;
                }
            }

            if (testSdk.TryGetPropertyValue<MethodInfo>("TestMethod", out MethodInfo testMethod))
            {
                testName = testMethod.Name;
            }
            else
            {
                // if we don't have the test method info, we can't extract the info that we need.
                Log.TestMethodNotFound();
                return null;
            }

            AssemblyName testInvokerAssemblyName = testSdk.GetType().Assembly.GetName();
            AssemblyName testClassInstanceAssemblyName = testClassType.Assembly?.GetName();

            testSuite = testClassType.ToString();

            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                if (testSdk.TryGetPropertyValue<object[]>("TestMethodArguments", out object[] testMethodArguments))
                {
                    testArguments = new List<KeyValuePair<string, string>>();

                    for (int i = 0; i < methodParameters.Length; i++)
                    {
                        if (i < testMethodArguments.Length)
                        {
                            testArguments.Add(new KeyValuePair<string, string>($"{TestTags.Arguments}.{methodParameters[i].Name}", testMethodArguments[i]?.ToString() ?? "(null)"));
                        }
                        else
                        {
                            testArguments.Add(new KeyValuePair<string, string>($"{TestTags.Arguments}.{methodParameters[i].Name}", "(default)"));
                        }
                    }
                }
            }

            Tracer tracer = Tracer.Instance;
            string serviceName = testClassInstanceAssemblyName?.Name ?? tracer.DefaultServiceName;

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(testName, serviceName: serviceName);
                Span span = scope.Span;
                span.Type = SpanTypes.Test;
                span.SetTraceSamplingPriority(SamplingPriority.UserKeep);
                span.ResourceName = testSuite;
                span.SetTag(TestTags.Suite, testSuite);
                span.SetTag(TestTags.Name, testName);
                span.SetTag(TestTags.Framework, "xUnit " + testInvokerAssemblyName.Version.ToString());
                span.SetMetric(Tags.Analytics, 1.0d);

                if (testArguments != null)
                {
                    foreach (KeyValuePair<string, string> argument in testArguments)
                    {
                        span.SetTag(argument.Key, argument.Value);
                    }
                }

                if (skipReason != null)
                {
                    span.SetTag(TestTags.Status, TestTags.StatusSkip);
                    span.SetTag(TestTags.SkipReason, skipReason);
                    scope.Dispose();
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
