// <copyright file="NUnitCompositeWorkItemSkipChildrenIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

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
    IntegrationName = NUnitIntegration.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class NUnitCompositeWorkItemSkipChildrenIntegration
{
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
    internal static CallTargetState OnMethodBegin<TTarget, TSuite, TResultState>(TTarget instance, TSuite testSuite, TResultState resultState, string message)
    {
        if (testSuite?.GetType() is { Name: { } typeName })
        {
            const string startString = "OneTimeSetUp:";
            message ??= string.Empty;
            if (message.StartsWith(startString, StringComparison.OrdinalIgnoreCase) == true)
            {
                message = message.Substring(startString.Length).Trim();
            }

            if (typeName == "CompositeWorkItem" && testSuite.TryDuckCast<ICompositeWorkItem>(out var compositeWorkItem))
            {
                // In case we have a CompositeWorkItem we check if there is a setup or teardown failure
                if (compositeWorkItem.Result.ResultState.Status == TestStatus.Failed || message.Contains("Exception:"))
                {
                    if (compositeWorkItem.Result.ResultState.Site == FailureSite.SetUp)
                    {
                        NUnitIntegration.WriteSetUpOrTearDownError(compositeWorkItem, "SetUpException");
                    }
                    else if (compositeWorkItem.Result.ResultState.Site == FailureSite.TearDown)
                    {
                        NUnitIntegration.WriteSetUpOrTearDownError(compositeWorkItem, "TearDownException");
                    }
                    else if (message.Contains("Exception:"))
                    {
                        NUnitIntegration.WriteSetUpOrTearDownError(compositeWorkItem, "Exception");
                    }

                    return CallTargetState.GetDefault();
                }
            }

            return new CallTargetState(null, new object[] { typeName, testSuite, message });
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>Return value of the method</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        if (state.State != null)
        {
            var stateArray = (object[])state.State;
            var typeName = (string)stateArray[0];
            var testSuiteOrWorkItem = stateArray[1];
            var skipMessage = (string)stateArray[2] ?? string.Empty;

            if (typeName == "ParameterizedMethodSuite" && testSuiteOrWorkItem.TryDuckCast<ITestSuite>(out var testSuite))
            {
                // In case the TestSuite is a ParameterizedMethodSuite instance
                foreach (var item in testSuite.Tests)
                {
                    if (item.TryDuckCast<ITest>(out var iTestItem) && NUnitIntegration.CreateTest(iTestItem) is { } test)
                    {
                        test.Close(Ci.TestStatus.Skip, TimeSpan.Zero, skipMessage);
                    }
                }
            }
            else if (typeName == "CompositeWorkItem" && testSuiteOrWorkItem.TryDuckCast<ICompositeWorkItem>(out var compositeWorkItem))
            {
                // In case we have a CompositeWorkItem
                foreach (var item in compositeWorkItem.Children)
                {
                    if (item.TryDuckCast<IWorkItem>(out var testResult))
                    {
                        NUnitIntegration.WriteSkip(testResult.Test, skipMessage);
                    }
                }
            }
        }

        return default;
    }
}
