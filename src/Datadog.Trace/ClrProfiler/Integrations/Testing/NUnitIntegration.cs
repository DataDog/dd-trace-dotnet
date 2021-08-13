// <copyright file="NUnitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    /// <summary>
    /// Tracing integration for NUnit teting framework
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NUnitIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.NUnit);
        private const string Major3 = "3";
        private const string Major3Minor0 = "3.0";

        private const string NUnitAssembly = "nunit.framework";

        private const string NUnitTestCommandType = "NUnit.Framework.Internal.Commands.TestCommand";
        private const string NUnitTestMethodCommandType = "NUnit.Framework.Internal.Commands.TestMethodCommand";
        private const string NUnitSkipCommandType = "NUnit.Framework.Internal.Commands.SkipCommand";
        private const string NUnitExecuteMethod = "Execute";

        private const string NUnitWorkShiftType = "NUnit.Framework.Internal.Execution.WorkShift";
        private const string NUnitShutdownMethod = "ShutDown";

        private const string NUnitTestAssemblyRunnerType = "NUnit.Framework.Api.NUnitTestAssemblyRunner";
        private const string WaitForCompletionMethod = "WaitForCompletion";

        private const string NUnitTestResultType = "NUnit.Framework.Internal.TestResult";
        private const string NUnitTestExecutionContextType = "NUnit.Framework.Internal.TestExecutionContext";

        private const string NUnitCompositeWorkItemType = "NUnit.Framework.Internal.Execution.CompositeWorkItem";
        private const string NUnitSkipChildrenMethod = "SkipChildren";
        private const string NUnitTestSuiteType = "NUnit.Framework.Internal.TestSuite";
        private const string NUnitResultStateType = "NUnit.Framework.Interfaces.ResultState";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitIntegration));
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

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
            AutoInstrumentation.Testing.Common.FlushSpans(IntegrationId);
        }

        /// <summary>
        /// NUnit.Framework.Api.NUnitTestAssemblyRunner.WaitForCompletion() instrumentation
        /// </summary>
        /// <param name="testAssemblyRunner">The NUnitTestAssembly instance</param>
        /// <param name="timeout">The timeout</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = NUnitAssembly,
            TargetType = NUnitTestAssemblyRunnerType,
            TargetMethod = WaitForCompletionMethod,
            TargetMinimumVersion = Major3Minor0,
            TargetMaximumVersion = Major3,
            TargetSignatureTypes = new[] { ClrNames.Bool, ClrNames.Int32 })]
        public static object NUnitTestAssemblyRunner_WaitForCompletion(
            object testAssemblyRunner,
            int timeout,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (testAssemblyRunner == null) { throw new ArgumentNullException(nameof(testAssemblyRunner)); }

            Type testAssemblyRunnerType = testAssemblyRunner.GetType();
            Func<object, int, object> execute;

            try
            {
                execute = MethodBuilder<Func<object, int, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, WaitForCompletionMethod)
                    .WithConcreteType(testAssemblyRunnerType)
                    .WithParameters(timeout)
                    .WithNamespaceAndNameFilters(ClrNames.Bool, ClrNames.Int32)
                    .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NUnitTestAssemblyRunnerType,
                    methodName: WaitForCompletionMethod,
                    instanceType: testAssemblyRunnerType.AssemblyQualifiedName);
                throw;
            }

            object result = execute(testAssemblyRunner, timeout);
            AutoInstrumentation.Testing.Common.FlushSpans(IntegrationId);
            return result;
        }

        /// <summary>
        /// Wrap the original NUnit.Framework.Internal.Execution.CompositeWorkItem.SkipChildren method by adding instrumentation code arount it
        /// </summary>
        /// <param name="compositeWorkItem">The CompositeWorkItem instance</param>
        /// <param name="testSuiteOrCompositeWorkItem">The test suite or CompositeWorkItem instance</param>
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
            TargetSignatureTypes = new[] { ClrNames.Void, "_", NUnitResultStateType, ClrNames.String })]
        public static void CompositeWorkItem_SkipChildren(
            object compositeWorkItem,
            object testSuiteOrCompositeWorkItem,
            object resultState,
            object message,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (compositeWorkItem == null) { throw new ArgumentNullException(nameof(compositeWorkItem)); }
            if (testSuiteOrCompositeWorkItem == null) { throw new ArgumentNullException(nameof(testSuiteOrCompositeWorkItem)); }

            Type testSuiteOrCompositeWorkItemType = testSuiteOrCompositeWorkItem.GetType();
            string argTypeName = testSuiteOrCompositeWorkItemType.Name == "CompositeWorkItem" ? NUnitCompositeWorkItemType : NUnitTestSuiteType;

            Type compositeWorkItemType = compositeWorkItem.GetType();
            Action<object, object, object, object> execute;

            try
            {
                execute = MethodBuilder<Action<object, object, object, object>>
                    .Start(moduleVersionPtr, mdToken, opCode, NUnitSkipChildrenMethod)
                    .WithConcreteType(compositeWorkItemType)
                    .WithParameters(testSuiteOrCompositeWorkItem, resultState, message)
                    .WithNamespaceAndNameFilters(ClrNames.Void, argTypeName, NUnitResultStateType, ClrNames.String)
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

            execute(compositeWorkItem, testSuiteOrCompositeWorkItem, resultState, message);

            var skipMessage = (string)message;
            const string startString = "OneTimeSetUp:";
            if (skipMessage?.StartsWith(startString, StringComparison.OrdinalIgnoreCase) == true)
            {
                skipMessage = skipMessage.Substring(startString.Length).Trim();
            }

            string typeName = testSuiteOrCompositeWorkItemType.Name;
            if (typeName == "ParameterizedMethodSuite" && testSuiteOrCompositeWorkItem.TryDuckCast<ITestSuite>(out var tSuite))
            {
                // In case the TestSuite is a ParameterizedMethodSuite instance
                foreach (var item in tSuite.Tests)
                {
                    Scope scope = AutoInstrumentation.Testing.NUnit.NUnitIntegration.CreateScope(item.DuckCast<ITest>(), compositeWorkItemType);
                    AutoInstrumentation.Testing.NUnit.NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                }
            }
            else if (typeName == "CompositeWorkItem" && testSuiteOrCompositeWorkItem.TryDuckCast<ICompositeWorkItem>(out var wItem))
            {
                // In case we have a CompositeWorkItem
                foreach (var item in wItem.Children)
                {
                    Scope scope = AutoInstrumentation.Testing.NUnit.NUnitIntegration.CreateScope(item.DuckCast<IWorkItem>().Result.Test, compositeWorkItemType);
                    AutoInstrumentation.Testing.NUnit.NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                }
            }
        }
    }
}
