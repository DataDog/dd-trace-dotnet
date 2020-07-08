using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
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

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(XUnitIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
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

            var currentContext = SynchronizationContext.Current;

            var scope = CreateScope(testInvoker, testClassInstance);
            if (scope is null)
            {
                return execute(testInvoker, testClassInstance);
            }

            object returnValue;
            try
            {
                returnValue = execute(testInvoker, testClassInstance);
                if (returnValue is Task returnTask)
                {
                    // TODO: Propertly await or set a continuation task with the same returnType (ValueTask, ValueTask<T>, Task or Task<T>)
                    // meanwhile we remove the syncronization context to avoid deadlocks
                    SynchronizationContext.SetSynchronizationContext(null);
                    returnTask.GetAwaiter().GetResult();
                }

                scope.Span.SetTag(TestTags.Status, TestTags.StatusPass);
            }
            catch (Exception ex)
            {
                scope.Span.SetException(ex);
                scope.Span.SetTag(TestTags.Status, TestTags.StatusFail);
                throw;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(currentContext);
                scope.Dispose();
            }

            return returnValue;
        }

        private static Scope CreateScope(object testInvoker, object testClassInstance)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string testSuite = null;
            string testName = null;
            List<KeyValuePair<string, string>> testArguments = null;

            if (testInvoker.TryGetPropertyValue<MethodInfo>("TestMethod", out MethodInfo testMethod))
            {
                testName = testMethod.Name;
            }
            else
            {
                // if we don't have the test method info, we can't extract the info that we need.
                Log.TestMethodNotFound();
                return null;
            }

            AssemblyName testInvokerAssemblyName = testInvoker.GetType().Assembly.GetName();
            AssemblyName testClassInstanceAssemblyName = testClassInstance.GetType().Assembly?.GetName();
            Type testClassInstanceType = testClassInstance.GetType();

            testSuite = testClassInstanceType.ToString();

            ParameterInfo[] methodParameters = testMethod.GetParameters();
            if (methodParameters?.Length > 0)
            {
                if (testInvoker.TryGetPropertyValue<object[]>("TestMethodArguments", out object[] testMethodArguments))
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
                scope = tracer.StartActive("Test", serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.Test;
                span.ResourceName = $"{testSuite}.{testName}";
                span.SetTag(TestTags.Suite, testSuite);
                span.SetTag(TestTags.Name, testName);
                span.SetTag(TestTags.Framework, "xUnit " + testInvokerAssemblyName.Version.ToString());

                if (testArguments != null)
                {
                    foreach (KeyValuePair<string, string> argument in testArguments)
                    {
                        span.SetTag(argument.Key, argument.Value);
                    }
                }

                span.SetMetric(Tags.Analytics, 1.0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
