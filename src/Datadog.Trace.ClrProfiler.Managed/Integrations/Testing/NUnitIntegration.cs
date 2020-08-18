using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        private const string NUnitTestCommandType = "NUnit.Framework.Internal.Commands.TestCommand";
        private const string NUnitTestMethodCommandType = "NUnit.Framework.Internal.Commands.TestMethodCommand";
        private const string NUnitSkipCommandType = "NUnit.Framework.Internal.Commands.SkipCommand";
        private const string NUnitExecuteMethod = "Execute";

        private const string NUnitWorkShiftType = "NUnit.Framework.Internal.Execution.WorkShift";
        private const string NUnitShutdownMethod = "ShutDown";

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
            TargetType = NUnitTestCommandType,
            TargetMethod = NUnitExecuteMethod,
            TargetMinimumVersion = Major3Minor0,
            TargetMaximumVersion = Major3,
            TargetSignatureTypes = new[] { NUnitTestResultType, NUnitTestExecutionContextType })]
        public static object TestCommand_Execute(
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
                    instrumentedType: NUnitTestCommandType,
                    methodName: NUnitExecuteMethod,
                    instanceType: testMethodCommandType.AssemblyQualifiedName);
                throw;
            }

            if (testMethodCommandType.FullName != NUnitTestMethodCommandType &&
                testMethodCommandType.FullName != NUnitSkipCommandType)
            {
                return execute(testMethodCommand, testExecutionContext);
            }

            bool skipped = testMethodCommandType.FullName == NUnitSkipCommandType;

            Log.Information("Executing: " + testMethodCommandType);
            return execute(testMethodCommand, testExecutionContext);
        }

        /// <summary>
        /// Wrap the original NUnit.Framework.Internal.Execution.WorkShift.ShutDown method by adding instrumentation code around it
        /// </summary>
        /// <param name="workShift">The workshift instance</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = NUnitAssembly,
            TargetType = NUnitWorkShiftType,
            TargetMethod = NUnitShutdownMethod,
            TargetMinimumVersion = Major3Minor0,
            TargetMaximumVersion = Major3,
            TargetSignatureTypes = new[] { ClrNames.Void })]
        public static void WorkShift_ShutDown(
            object workShift,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (workShift == null) { throw new ArgumentNullException(nameof(workShift)); }

            Type workShiftType = workShift.GetType();
            Action<object> execute;

            try
            {
                execute = MethodBuilder<Action<object>>
                    .Start(moduleVersionPtr, mdToken, opCode, NUnitShutdownMethod)
                    .WithConcreteType(workShiftType)
                    .WithNamespaceAndNameFilters(ClrNames.Void)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NUnitWorkShiftType,
                    methodName: NUnitShutdownMethod,
                    instanceType: workShiftType.AssemblyQualifiedName);
                throw;
            }

            Log.Information("Executing: " + workShiftType);
            execute(workShift);
            SynchronizationContext context = SynchronizationContext.Current;
            try
            {
                // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                // So the last spans in buffer aren't send to the agent.
                // Other times we reach the 500 items of the buffer in a sec and the tracer start to drop spans.
                // In a test scenario we must keep all spans.
                Tracer.Instance.FlushAsync().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }
        }
    }
}
