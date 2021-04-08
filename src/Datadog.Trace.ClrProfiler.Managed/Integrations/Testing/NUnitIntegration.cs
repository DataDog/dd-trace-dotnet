using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
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

        private const string NUnitCompositeWorkItemType = "NUnit.Framework.Internal.Execution.CompositeWorkItem";
        private const string NUnitSkipChildrenMethod = "SkipChildren";
        private const string NUnitTestSuiteType = "NUnit.Framework.Internal.TestSuite";
        private const string NUnitResultStateType = "NUnit.Framework.Interfaces.ResultState";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));

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

            Scope scope = null;

            if (testExecutionContext.TryDuckCast<ITestExecutionContext>(out var testExCtx))
            {
                scope = AutoInstrumentation.Testing.NUnit.NUnitIntegration.CreateScope(testExCtx.CurrentTest, testMethodCommandType);
            }

            if (scope is null)
            {
                return execute(testMethodCommand, testExecutionContext);
            }

            using (scope)
            {
                object result = null;
                Exception exception = null;
                try
                {
                    scope.Span.ResetStartTime();
                    result = execute(testMethodCommand, testExecutionContext);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    throw;
                }
                finally
                {
                    AutoInstrumentation.Testing.NUnit.NUnitIntegration.FinishScope(scope, exception);
                }

                return result;
            }
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

            execute(workShift);
            SynchronizationContext context = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                // So the last spans in buffer aren't send to the agent.
                // Other times we reach the 500 items of the buffer in a sec and the tracer start to drop spans.
                // In a test scenario we must keep all spans.
                Common.TestTracer.FlushAsync().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }
        }

        /// <summary>
        /// Wrap the original NUnit.Framework.Internal.Execution.CompositeWorkItem.SkipChildren method by adding instrumentation code arount it
        /// </summary>
        /// <param name="compositeWorkItem">The CompositeWorkItem instance</param>
        /// <param name="testSuite">The test suite instance</param>
        /// <param name="resultState">the result state instance</param>
        /// <param name="message">The message instance</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = NUnitAssembly,
            TargetType = NUnitCompositeWorkItemType,
            TargetMethod = NUnitSkipChildrenMethod,
            TargetMinimumVersion = Major3Minor0,
            TargetMaximumVersion = Major3,
            TargetSignatureTypes = new[] { ClrNames.Void, NUnitTestSuiteType, NUnitResultStateType, ClrNames.String })]
        public static void CompositeWorkItem_SkipChildren(
            object compositeWorkItem,
            object testSuite,
            object resultState,
            object message,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (compositeWorkItem == null) { throw new ArgumentNullException(nameof(compositeWorkItem)); }

            Type compositeWorkItemType = compositeWorkItem.GetType();
            Action<object, object, object, object> execute;

            try
            {
                execute = MethodBuilder<Action<object, object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, NUnitSkipChildrenMethod)
                    .WithConcreteType(compositeWorkItemType)
                    .WithParameters(testSuite, resultState, message)
                    .WithNamespaceAndNameFilters(ClrNames.Void, NUnitTestSuiteType, NUnitResultStateType, ClrNames.String)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NUnitCompositeWorkItemType,
                    methodName: NUnitSkipChildrenMethod,
                    instanceType: compositeWorkItemType.AssemblyQualifiedName);
                throw;
            }

            execute(compositeWorkItem, testSuite, resultState, message);

            if (testSuite.TryDuckCast<ITestSuite>(out var tSuite))
            {
                var skipMessage = (string)message;
                const string startString = "OneTimeSetUp:";
                if (skipMessage?.StartsWith(startString, StringComparison.OrdinalIgnoreCase) == true)
                {
                    skipMessage = skipMessage.Substring(startString.Length).Trim();
                }

                foreach (var item in tSuite.Tests)
                {
                    Scope scope = AutoInstrumentation.Testing.NUnit.NUnitIntegration.CreateScope(item.DuckCast<ITest>(), compositeWorkItemType);
                    AutoInstrumentation.Testing.NUnit.NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                }
            }
        }
    }
}
