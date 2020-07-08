using System;
using System.Net;
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

            Console.WriteLine("CallTestMethod interception: " + testInvokerType.ToString());
            Log.Warning("CallTestMethod interception: " + testInvokerType.ToString());

            Func<object, object, object> execute;

            try
            {
                execute =
                    MethodBuilder<Func<object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, XUnitCallTestMethod)
                       .WithConcreteType(testInvokerType)
                       .WithExplicitParameterTypes(typeof(object))
                       // .WithParameters(testClassInstance)
                       // .WithNamespaceAndNameFilters(ClrNames.Object, ClrNames.Object)
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

            return execute(testInvoker, testClassInstance);
        }
    }
}
