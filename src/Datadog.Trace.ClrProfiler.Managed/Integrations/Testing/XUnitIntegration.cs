using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// Tracing integration for XUnit testing framework
    /// </summary>
    public static class XUnitIntegration
    {
        private const string Major2 = "2";
        private const string Major2Minor2 = "2.2";

        private const string XUnitNetCoreAssembly = "xunit.execution.dotnet";
        private const string XUnitDesktopAssembly = "xunit.execution.desktop";

        private const string XUnitTestInvokerType = "Xunit.Sdk.TestInvoker`1";
        private const string XUnitTestRunnerType = "Xunit.Sdk.TestRunner`1";
        private const string XUnitTestAssemblyRunnerType = "Xunit.Sdk.TestAssemblyRunner`1";
        private const string XUnitTestOutputHelperType = "Xunit.Sdk.TestOutputHelper";

        private const string XUnitRunAsyncMethod = "RunAsync";
        private const string XUnitRunTestCollectionAsyncMethod = "RunTestCollectionAsync";
        private const string XUnitQueueTestOutputMethod = "QueueTestOutput";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.XUnit));
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(XUnitIntegration));
        private static readonly FrameworkDescription RuntimeDescription;

        static XUnitIntegration()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
            RuntimeDescription = FrameworkDescription.Instance;
        }

        /// <summary>
        /// Wrap the original Xunit.Sdk.TestInvoker`1.RunAsync method by adding instrumentation code around it.
        /// </summary>
        /// <param name="testInvoker">The TestInvoker instance we are replacing.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { XUnitNetCoreAssembly, XUnitDesktopAssembly },
            TargetType = XUnitTestInvokerType,
            TargetMethod = XUnitRunAsyncMethod,
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Decimal>" })]
        public static object TestInvoker_RunAsync(
            object testInvoker,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testInvoker == null) { throw new ArgumentNullException(nameof(testInvoker)); }

            Type testInvokerType = testInvoker.GetType();
            Func<object, object> executeAsync;

            try
            {
                executeAsync =
                    MethodBuilder<Func<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, XUnitRunAsyncMethod)
                       .WithConcreteType(testInvokerType)
                       .WithDeclaringTypeGenerics(testInvokerType.BaseType.GenericTypeArguments)
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
                    instrumentedType: XUnitTestInvokerType,
                    methodName: XUnitRunAsyncMethod,
                    instanceType: testInvokerType.AssemblyQualifiedName);
                throw;
            }

            object returnValue = null;
            Exception exception = null;
            try
            {
                returnValue = executeAsync(testInvoker);
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
                returnValue = AsyncTool.AddContinuation(returnValue, exception, testInvoker, (r, ex, state) => InvokerContinuation(r, ex, state));
            }

            return returnValue;
        }

        private static object InvokerContinuation(object returnValue, Exception ex, object state)
        {
            if (state.TryGetPropertyValue<object>("Aggregator", out object aggregator))
            {
                if (aggregator.TryCallMethod<Exception>("ToException", out Exception testException))
                {
                    Span span = Tracer.Instance?.ActiveScope?.Span;
                    if (span != null && testException != null)
                    {
                        span.SetException(testException);
                        span.SetTag(TestTags.Status, TestTags.StatusFail);
                    }
                }
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
            TargetAssemblies = new[] { XUnitNetCoreAssembly, XUnitDesktopAssembly },
            TargetType = XUnitTestRunnerType,
            TargetMethod = XUnitRunAsyncMethod,
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>" })]
        public static object TestRunner_RunAsync(
            object testRunner,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testRunner == null) { throw new ArgumentNullException(nameof(testRunner)); }

            Type testRunnerType = testRunner.GetType();
            Func<object, object> executeAsync;

            try
            {
                executeAsync =
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

            Scope scope = CreateScope(testRunner);
            if (scope is null)
            {
                return executeAsync(testRunner);
            }

            object returnValue = null;
            Exception exception = null;
            try
            {
                // reset the start time of the span just before running the test
                scope.Span.ResetStartTime();

                // starts the test execution
                returnValue = executeAsync(testRunner);
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
                returnValue = AsyncTool.AddContinuation(returnValue, exception, scope, (r, ex, state) => TestRunnerContinuation(r, ex, state));
            }

            return returnValue;
        }

        private static object TestRunnerContinuation(object returnValue, Exception ex, Scope scope)
        {
            if (scope.Span.GetTag(TestTags.Status) == null)
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
            }

            scope.Dispose();
            return returnValue;
        }

        /// <summary>
        /// Wrap the original Xunit.Sdk.XunitTestAssemblyRunner.BeforeTestAssemblyFinishedAsync method by adding instrumentation code around it
        /// </summary>
        /// <param name="xunitTestAssemblyRunner">The XunitTestAssemblyRunner instance we are replacing.</param>
        /// <param name="messageBus">Message bus instance</param>
        /// <param name="testCollection">Test collection instance</param>
        /// <param name="testCases">Test cases instance</param>
        /// <param name="cancellationTokenSource">Cancellation token source</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { XUnitNetCoreAssembly, XUnitDesktopAssembly },
            TargetType = XUnitTestAssemblyRunnerType,
            TargetMethod = XUnitRunTestCollectionAsyncMethod,
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Xunit.Sdk.RunSummary>", "Xunit.Sdk.IMessageBus", "Xunit.Abstractions.ITestCollection", "System.Collections.Generic.IEnumerable`1<T>", "System.Threading.CancellationTokenSource" })]
        public static object AssemblyRunner_RunAsync(
            object xunitTestAssemblyRunner,
            object messageBus,
            object testCollection,
            object testCases,
            object cancellationTokenSource,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (xunitTestAssemblyRunner == null) { throw new ArgumentNullException(nameof(xunitTestAssemblyRunner)); }

            Type xunitTestAssemblyRunnerType = xunitTestAssemblyRunner.GetType();
            Func<object, object, object, object, object, object> executeAsync;

            try
            {
                executeAsync =
                    MethodBuilder<Func<object, object, object, object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, XUnitRunTestCollectionAsyncMethod)
                       .WithConcreteType(xunitTestAssemblyRunnerType)
                       .WithParameters(messageBus, testCollection, testCases, cancellationTokenSource)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, "Xunit.Sdk.IMessageBus", "Xunit.Abstractions.ITestCollection", "System.Collections.Generic.IEnumerable`1<T>", "System.Threading.CancellationTokenSource")
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: XUnitTestAssemblyRunnerType,
                    methodName: XUnitRunTestCollectionAsyncMethod,
                    instanceType: xunitTestAssemblyRunnerType.AssemblyQualifiedName);
                throw;
            }

            object returnValue = null;
            Exception exception = null;
            try
            {
                returnValue = executeAsync(xunitTestAssemblyRunner, messageBus, testCollection, testCases, cancellationTokenSource);
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
                returnValue = AsyncTool.AddContinuation<object>(
                    returnValue,
                    exception,
                    null,
                    async (r, ex, state) =>
                    {
                        // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                        // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                        // So the last spans in buffer aren't send to the agent.
                        // Other times we reach the 500 items of the buffer in a sec and the tracer start to drop spans.
                        // In a test scenario we must keep all spans.
                        await Tracer.Instance.FlushAsync().ConfigureAwait(false);
                        return r;
                    });
            }

            return returnValue;
        }

        private static Scope CreateScope(object testSdk)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;
            try
            {
                string testSuite = null;
                string testName = null;
                string skipReason = null;
                List<KeyValuePair<string, string>> testArguments = null;
                List<KeyValuePair<string, string>> testTraits = null;

                // Get test type
                if (!testSdk.TryGetPropertyValue<Type>("TestClass", out Type testClassType))
                {
                    // if we don't have the test class type, we can't extract the info that we need.
                    Log.TestClassTypeNotFound();
                    return null;
                }

                // Get test method
                if (!testSdk.TryGetPropertyValue<MethodInfo>("TestMethod", out MethodInfo testMethod))
                {
                    // if we don't have the test method info, we can't extract the info that we need.
                    Log.TestMethodNotFound();
                    return null;
                }

                // Get test name
                testName = testMethod.Name;

                // Get skip reason
                testSdk.TryGetPropertyValue<string>("SkipReason", out skipReason);

                // Get traits
                if (testSdk.TryGetPropertyValue("TestCase", out object testCase))
                {
                    if (testCase.TryGetPropertyValue<Dictionary<string, List<string>>>("Traits", out Dictionary<string, List<string>> traits) && traits != null)
                    {
                        if (traits.Count > 0)
                        {
                            testTraits = new List<KeyValuePair<string, string>>();

                            foreach (KeyValuePair<string, List<string>> traitValue in traits)
                            {
                                testTraits.Add(new KeyValuePair<string, string>($"{TestTags.Traits}.{traitValue.Key}", string.Join(", ", traitValue.Value) ?? "(null)"));
                            }
                        }
                    }
                }

                AssemblyName testInvokerAssemblyName = testSdk.GetType().Assembly.GetName();
                AssemblyName testClassInstanceAssemblyName = testClassType.Assembly?.GetName();

                testSuite = testClassType.ToString();

                // Get test parameters
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
                string testFramework = "xUnit " + testInvokerAssemblyName.Version.ToString();

                scope = tracer.StartActive("xunit.test");
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

                span.SetTag(CommonTags.RuntimeName, RuntimeDescription.Name);
                span.SetTag(CommonTags.RuntimeOSArchitecture, RuntimeDescription.OSArchitecture);
                span.SetTag(CommonTags.RuntimeOSPlatform, RuntimeDescription.OSPlatform);
                span.SetTag(CommonTags.RuntimeProcessArchitecture, RuntimeDescription.ProcessArchitecture);
                span.SetTag(CommonTags.RuntimeVersion, RuntimeDescription.ProductVersion);

                if (testArguments != null)
                {
                    foreach (KeyValuePair<string, string> argument in testArguments)
                    {
                        span.SetTag(argument.Key, argument.Value);
                    }
                }

                if (testTraits != null)
                {
                    foreach (KeyValuePair<string, string> trait in testTraits)
                    {
                        span.SetTag(trait.Key, trait.Value);
                    }
                }

                if (skipReason != null)
                {
                    span.SetTag(TestTags.Status, TestTags.StatusSkip);
                    span.SetTag(TestTags.SkipReason, skipReason);
                    span.Finish(TimeSpan.Zero);
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

        /// <summary>
        /// Wrap the original Xunit.Sdk.TestOutputHelper.QueueTestOutput to add the TraceId and SpanId prefix to all outputs.
        /// </summary>
        /// <param name="testOutputHelper">The Xunit.Sdk.TestOutputHelper instance</param>
        /// <param name="output">The string output instance</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssemblies = new[] { XUnitNetCoreAssembly, XUnitDesktopAssembly },
            TargetType = XUnitTestOutputHelperType,
            TargetMethod = XUnitQueueTestOutputMethod,
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2,
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.String })]
        public static void TestOutputHelper_QueueTestOutput(
            object testOutputHelper,
            object output,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testOutputHelper == null) { throw new ArgumentNullException(nameof(testOutputHelper)); }

            Type testOutputHelperType = testOutputHelper.GetType();
            Action<object, object> execute;

            try
            {
                execute =
                    MethodBuilder<Action<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, XUnitQueueTestOutputMethod)
                       .WithConcreteType(testOutputHelperType)
                       .WithParameters(output)
                       .WithNamespaceAndNameFilters(ClrNames.Void, ClrNames.String)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: XUnitTestOutputHelperType,
                    methodName: XUnitQueueTestOutputMethod,
                    instanceType: testOutputHelperType.AssemblyQualifiedName);
                throw;
            }

            output = $"[{CorrelationIdentifier.TraceIdKey}={CorrelationIdentifier.TraceId},{CorrelationIdentifier.SpanIdKey}={CorrelationIdentifier.SpanId}]{output}";
            execute(testOutputHelper, output);
        }
    }
}
