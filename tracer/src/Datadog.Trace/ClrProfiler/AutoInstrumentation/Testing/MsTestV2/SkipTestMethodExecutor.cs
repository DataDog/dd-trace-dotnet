// <copyright file="SkipTestMethodExecutor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal class SkipTestMethodExecutor
{
    private readonly object _arrayInstance;

    public SkipTestMethodExecutor(Assembly assembly)
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
        return _arrayInstance;
    }
}
