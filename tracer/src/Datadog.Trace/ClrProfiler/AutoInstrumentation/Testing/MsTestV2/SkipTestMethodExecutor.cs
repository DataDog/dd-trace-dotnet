// <copyright file="SkipTestMethodExecutor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal class SkipTestMethodExecutor
{
    private readonly object _arrayInstance;
    private readonly string _skipReason;

    public SkipTestMethodExecutor(Assembly assembly, string skipReason)
    {
        var testResultType = assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.TestResult", throwOnError: true)!;
        var array = Array.CreateInstance(testResultType, 1);
        var result = Activator.CreateInstance(testResultType);
        if (DuckType.Create<ITestResult>(result) is { } iResult)
        {
            iResult.Outcome = UnitTestOutcome.Inconclusive; // Inconclusive is reported as Skipped in the CLI
        }

        array.SetValue(result, 0);
        _arrayInstance = array;
        _skipReason = skipReason;
    }

    [DuckReverseMethod(Name = "Execute", ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"])]
    public object Execute(object testMethod)
    {
        if (testMethod.TryDuckCast<ITestMethod>(out var testMethodInfo))
        {
            // Create the skip span
            MsTestIntegration.OnMethodBegin(testMethodInfo, testMethod.GetType(), isRetry: false)?
               .Close(TestStatus.Skip, TimeSpan.Zero, _skipReason);
        }

        return _arrayInstance;
    }
}

#pragma warning disable SA1402
internal class SkipTestMethodExecutorAsync
#pragma warning restore SA1402
{
    private readonly object _resultInstance;
    private readonly string _skipReason;

    public SkipTestMethodExecutorAsync(Assembly assembly, string skipReason)
    {
        var testResultType = assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.TestResult", throwOnError: true)!;
        var array = Array.CreateInstance(testResultType, 1);
        var result = Activator.CreateInstance(testResultType);
        if (DuckType.Create<ITestResult>(result) is { } iResult)
        {
            iResult.Outcome = UnitTestOutcome.Inconclusive; // Inconclusive is reported as Skipped in the CLI
        }

        array.SetValue(result, 0);
        _skipReason = skipReason;
        _resultInstance = ((TaskTestResultArray?)Activator.CreateInstance(typeof(TaskTestResultArray<>).MakeGenericType([array.GetType()])))!.Result;
    }

    [DuckReverseMethod(Name = "ExecuteAsync", ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"])]
    public object Execute(object testMethod)
    {
        if (testMethod.TryDuckCast<ITestMethod>(out var testMethodInfo))
        {
            // Create the skip span
            MsTestIntegration.OnMethodBegin(testMethodInfo, testMethod.GetType(), isRetry: false)?
               .Close(TestStatus.Skip, TimeSpan.Zero, _skipReason);
        }

        return _resultInstance;
    }

    private abstract class TaskTestResultArray
    {
        public abstract object Result { get; }
    }

    private class TaskTestResultArray<T>(T value) : TaskTestResultArray
    {
        public override object Result { get; } = Task.FromResult(value);
    }
}
