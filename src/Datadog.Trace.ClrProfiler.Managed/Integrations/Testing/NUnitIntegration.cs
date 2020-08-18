using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// Tracing integration for NUnit teting framework
    /// </summary>
    public static class NUnitIntegration
    {
        private const string IntegrationName = "NUnit";
        private const string Major3 = "3";
        private const string Major3Minor0 = "3.0";

        private const string NUnitAssembly = "nunit.framework";

        private const string NUnitTestMethodCommandType = "NUnit.Framework.Internal.Commands.TestMethodCommand";
        private const string NUnitSkipCommandType = "NUnit.Framework.Internal.Commands.SkipCommand";

        private const string NUnitExecuteMethod = "Execute";

        private const string NUnitTestResultType = "NUnit.Framework.Internal.TestResult";
        private const string NUnitTestExecutionContextType = "NUnit.Framework.Internal.TestExecutionContext";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(NUnitIntegration));
        private static readonly FrameworkDescription _runtimeDescription;

        static NUnitIntegration()
        {
            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);

            _runtimeDescription = FrameworkDescription.Create();
        }

        /// <summary>
        /// Wrap the original NUnit.Framework.Internal.Commands.TestMethodCommand.Execute method by adding instrumentation code around it
        /// </summary>
        /// <param name="testMethodCommand">The test method command instance</param>
        /// <param name="testExecutionContext">Test execution context</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = NUnitAssembly,
            TargetType = NUnitTestMethodCommandType,
            TargetMethod = NUnitExecuteMethod,
            TargetMinimumVersion = Major3Minor0,
            TargetMaximumVersion = Major3,
            TargetSignatureTypes = new[] { NUnitTestResultType, NUnitTestExecutionContextType })]
        public static object TestMethodCommand_Execute(
            object testMethodCommand,
            object testExecutionContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testMethodCommand == null) { throw new ArgumentNullException(nameof(testMethodCommand)); }

            Type testMethodCommandType = testMethodCommand.GetType();
            Func<object, object, object> execute;

            try
            {
                execute = MethodBuilder<Func<object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, NUnitExecuteMethod)
                    .WithConcreteType(testMethodCommandType)
                    .WithParameters(testExecutionContext)
                    .WithNamespaceAndNameFilters(NUnitTestResultType, NUnitTestExecutionContextType)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NUnitTestMethodCommandType,
                    methodName: NUnitExecuteMethod,
                    instanceType: testMethodCommandType.AssemblyQualifiedName);
                throw;
            }

            Log.Information("Executing: " + NUnitTestMethodCommandType);
            return execute(testMethodCommand, testExecutionContext);
        }

        /// <summary>
        /// Wrap the original NUnit.Framework.Internal.Commands.SkipCommand.Execute method by adding instrumentation code around it
        /// </summary>
        /// <param name="skipCommand">The skip command instance</param>
        /// <param name="testExecutionContext">Test execution context</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = NUnitAssembly,
            TargetType = NUnitSkipCommandType,
            TargetMethod = NUnitExecuteMethod,
            TargetMinimumVersion = Major3Minor0,
            TargetMaximumVersion = Major3,
            TargetSignatureTypes = new[] { NUnitTestResultType, NUnitTestExecutionContextType })]
        public static object SkipCommand_Execute(
            object skipCommand,
            object testExecutionContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (skipCommand == null) { throw new ArgumentNullException(nameof(skipCommand)); }

            Type skipCommandType = skipCommand.GetType();
            Func<object, object, object> execute;

            try
            {
                execute = MethodBuilder<Func<object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, NUnitExecuteMethod)
                    .WithConcreteType(skipCommandType)
                    .WithParameters(testExecutionContext)
                    .WithNamespaceAndNameFilters(NUnitTestResultType, NUnitTestExecutionContextType)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NUnitSkipCommandType,
                    methodName: NUnitExecuteMethod,
                    instanceType: skipCommandType.AssemblyQualifiedName);
                throw;
            }

            Log.Information("Executing: " + NUnitSkipCommandType);
            return execute(skipCommand, testExecutionContext);
        }
    }
}
