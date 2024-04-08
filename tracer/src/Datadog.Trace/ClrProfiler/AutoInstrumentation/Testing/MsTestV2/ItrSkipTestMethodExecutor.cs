// <copyright file="ItrSkipTestMethodExecutor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal class ItrSkipTestMethodExecutor
{
    private readonly object _arrayInstance;

    public ItrSkipTestMethodExecutor(Assembly assembly)
    {
        var testResultType = assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.TestResult", throwOnError: true);
        var array = Array.CreateInstance(testResultType, 1);
        var result = Activator.CreateInstance(testResultType);
        var iResult = DuckType.Create<ITestResult>(result);
        iResult.Outcome = UnitTestOutcome.Inconclusive; // Inconclusive is reported as Skipped in the CLI
        array.SetValue(result, 0);
        _arrayInstance = array;
    }

    [DuckReverseMethod(Name = "Execute", ParameterTypeNames = new[] { "Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod" })]
    public object Execute(object testMethod)
    {
        if (testMethod.TryDuckCast<ITestMethod>(out var testMethodInfo))
        {
            // Create ITR skip span
            MsTestIntegration.OnMethodBegin(testMethodInfo, testMethod.GetType(), isRetry: false)?
                             .Close(TestStatus.Skip, TimeSpan.Zero, IntelligentTestRunnerTags.SkippedByReason);
        }

        return _arrayInstance;
    }
}
