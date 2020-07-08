using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// Tracing integration for XUnit testing framework
    /// </summary>
    public static class XUnitIntegration
    {
        private const string IntegrationName = "XUnit";
        private const string ServiceName = "test";

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
            object returnValue;
            try
            {
                returnValue = execute(testInvoker, testClassInstance);
                if (returnValue is Task returnTask)
                {
                    // TODO: Propertly await or set a continuation task with the same returnType (Task or Task<T>)
                    SynchronizationContext.SetSynchronizationContext(null);
                    returnTask.GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                scope.Span.SetException(ex);
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

            testSuite = testClassInstance.GetType().ToString();
            if (testInvoker.TryGetPropertyValue<MethodInfo>("TestMethod", out MethodInfo testMethod))
            {
                testName = testMethod.Name;
            }

            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                scope = tracer.StartActive("Test", serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.Test;
                span.ResourceName = $"{testSuite}.{testName}";
                span.SetTag("test.suite", testSuite);
                span.SetTag("test.name", testName);
                span.SetTag("test.framework", "xUnit");
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
