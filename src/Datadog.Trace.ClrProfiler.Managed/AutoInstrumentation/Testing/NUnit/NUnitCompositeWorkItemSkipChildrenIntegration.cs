using System;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// NUnit.Framework.Internal.Execution.CompositeWorkItem.SkipChildren() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "nunit.framework",
        TypeName = "NUnit.Framework.Internal.Execution.CompositeWorkItem",
        MethodName = "SkipChildren",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "_", "NUnit.Framework.Interfaces.ResultState", ClrNames.String },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = IntegrationName)]
    public class NUnitCompositeWorkItemSkipChildrenIntegration
    {
        private const string IntegrationName = nameof(IntegrationIds.NUnit);
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NUnitCompositeWorkItemSkipChildrenIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TSuite">Test suite type</typeparam>
        /// <typeparam name="TResultState">Result state type</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="testSuite">Test suite instance</param>
        /// <param name="resultState">Result state instance</param>
        /// <param name="message">Message instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TSuite, TResultState>(TTarget instance, TSuite testSuite, TResultState resultState, string message)
        {
            return new CallTargetState(null, new object[] { testSuite, message });
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>Return value of the method</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            if (state.State != null)
            {
                object[] stateArray = (object[])state.State;
                string skipMessage = (string)stateArray[1];
                const string startString = "OneTimeSetUp:";
                if (skipMessage?.StartsWith(startString, StringComparison.OrdinalIgnoreCase) == true)
                {
                    skipMessage = skipMessage.Substring(startString.Length).Trim();
                }

                object testSuiteOrWorkItem = stateArray[0];

                if (testSuiteOrWorkItem is not null)
                {
                    string typeName = testSuiteOrWorkItem.GetType().Name;

                    if (typeName == "ParameterizedMethodSuite")
                    {
                        // In case the TestSuite is a ParameterizedMethodSuite instance
                        var testSuite = testSuiteOrWorkItem.DuckCast<ITestSuite>();
                        foreach (var item in testSuite.Tests)
                        {
                            Scope scope = NUnitIntegration.CreateScope(item.DuckCast<ITest>(), typeof(TTarget));
                            NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                        }
                    }
                    else if (typeName == "CompositeWorkItem")
                    {
                        // In case we have a CompositeWorkItem
                        var compositeWorkItem = testSuiteOrWorkItem.DuckCast<ICompositeWorkItem>();
                        foreach (var item in compositeWorkItem.Children)
                        {
                            Scope scope = NUnitIntegration.CreateScope(item.DuckCast<IWorkItem>().Result.Test, typeof(TTarget));
                            NUnitIntegration.FinishSkippedScope(scope, skipMessage);
                        }
                    }
                }
            }

            return default;
        }
    }
}
