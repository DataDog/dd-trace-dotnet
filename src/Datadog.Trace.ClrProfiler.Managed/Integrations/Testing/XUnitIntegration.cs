using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private const string XUnitTestAssemblyRunnerType = "Xunit.Sdk.TestAssemblyRunner`1";
        private const string XUnitRunTestCollectionAsyncMethod = "RunTestCollectionAsync";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(XUnitIntegration));

        private static long _testInvokerScopesCount;
        private static long _testInvokerScopesDisposedCount;
        private static long _testRunAsyncSkipped;

        private static FrameworkDescription _runtimeDescription;

        private static string _processId;

        static XUnitIntegration()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);

            _runtimeDescription = FrameworkDescription.Create();

            try
            {
                _processId = Process.GetCurrentProcess().Id.ToString();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error getting the process id.");
            }
        }

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
                return execute(testInvoker, testClassInstance);
            }

            Log.Debug($"** SCOPE ({scope.Span.Context.TraceId}) Created for TestInvoker. [{Interlocked.Increment(ref _testInvokerScopesCount)}:{Interlocked.Read(ref _testInvokerScopesDisposedCount)}:{Interlocked.Read(ref _testRunAsyncSkipped)}]");

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
                    Log.Debug($"** SCOPE ({scope.Span.Context.TraceId}) Disposed for TestInvoker. [{Interlocked.Read(ref _testInvokerScopesCount)}:{Interlocked.Increment(ref _testInvokerScopesDisposedCount)}:{Interlocked.Read(ref _testRunAsyncSkipped)}]");
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
        public static object TestRunner_RunAsync(
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
                Log.Debug($"** SCOPE Test SKIPPED. [{Interlocked.Increment(ref _testInvokerScopesCount)}:{Interlocked.Increment(ref _testInvokerScopesDisposedCount)}:{Interlocked.Increment(ref _testRunAsyncSkipped)}]");
            }

            return execute(testRunner);
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
            TargetAssembly = XUnitAssembly,
            TargetType = XUnitTestAssemblyRunnerType,
            TargetMethod = XUnitRunTestCollectionAsyncMethod,
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
            Func<object, object, object, object, object, object> execute;

            try
            {
                execute =
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
                returnValue = execute(xunitTestAssemblyRunner, messageBus, testCollection, testCases, cancellationTokenSource);
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
                returnValue = AsyncTool.AddContinuation(returnValue, exception, async (r, ex) =>
                {
                    // We have to ensure the flush of the buffer, for some reason, when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                    // So the last spans in buffer aren't send to the agent.
                    await Tracer.Instance.FlushAsync().ConfigureAwait(false);
                    return r;
                });
            }

            return returnValue;
        }

        private static Scope CreateScope(object testSdk, Type testClassType)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string uniqueId = null;
            string testSuite = null;
            string testName = null;
            string skipReason = null;
            List<KeyValuePair<string, string>> testArguments = null;
            List<KeyValuePair<string, string>> testTraits = null;

            if (testClassType is null)
            {
                // If testClassType is null, is because this method is called from an upper caller,
                // if we detect the SkipReason we write the Test skip span, if not we don't do nothing
                // and wait for the other caller.

                testSdk.TryGetPropertyValue<string>("SkipReason", out skipReason);
                if (skipReason == null)
                {
                    // no skip reason, so we do nothing.
                    return null;
                }

                if (!testSdk.TryGetPropertyValue<Type>("TestClass", out testClassType))
                {
                    // if we don't have the test class type, we can't extract the info that we need.
                    Log.TestClassTypeNotFound();
                    return null;
                }
            }

            // Get test method
            if (!testSdk.TryGetPropertyValue<MethodInfo>("TestMethod", out MethodInfo testMethod))
            {
                // if we don't have the test method info, we can't extract the info that we need.
                Log.TestMethodNotFound();
                return null;
            }

            testName = testMethod.Name;

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
                            testArguments.Add(new KeyValuePair<string, string>($"{TestTags.Traits}.{traitValue.Key}", string.Join(", ", traitValue.Value) ?? "(null)"));
                        }
                    }
                }

                testCase.TryGetPropertyValue<string>("UniqueID", out uniqueId);
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
            string serviceName = testClassInstanceAssemblyName?.Name ?? tracer.DefaultServiceName;
            string testFramework = "xUnit " + testInvokerAssemblyName.Version.ToString();

            Scope scope = null;
            try
            {
                scope = tracer.StartActive(testName, serviceName: serviceName, finishOnClose: skipReason == null);
                Span span = scope.Span;
                span.Type = SpanTypes.Test;
                span.SetTraceSamplingPriority(SamplingPriority.UserKeep);
                span.ResourceName = testSuite;
                span.SetTag(TestTags.Suite, testSuite);
                span.SetTag(TestTags.Name, testName);
                span.SetTag(TestTags.Fqn, $"{testSuite}.{testName}");
                span.SetTag(TestTags.Framework, testFramework);
                span.SetMetric(Tags.Analytics, 1.0d);
                CIEnvironmentValues.DecorateSpan(span);

                span.SetTag(TestTags.RuntimeName, _runtimeDescription.Name);
                span.SetTag(TestTags.RuntimeOSArchitecture, _runtimeDescription.OSArchitecture);
                span.SetTag(TestTags.RuntimeOSPlatform, _runtimeDescription.OSPlatform);
                span.SetTag(TestTags.RuntimeProcessArchitecture, _runtimeDescription.ProcessArchitecture);
                span.SetTag(TestTags.RuntimeVersion, _runtimeDescription.ProductVersion);

                if (uniqueId != null)
                {
                    span.SetTag(TestTags.Id, uniqueId);
                }

                if (_processId != null)
                {
                    span.SetTag(TestTags.ProcessId, _processId);
                }

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
    }
}
