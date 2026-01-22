// <copyright file="SkipTestMethodExecutor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal abstract class SkipTestMethodExecutor
{
    private readonly object _arrayInstance;
    private readonly string _skipReason;

    protected SkipTestMethodExecutor(Assembly assembly, string skipReason)
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

    protected void ProcessTestMethod(object testMethod)
    {
        if (testMethod.TryDuckCast<ITestMethod>(out var testMethodInfo))
        {
            // Create the skip span
            var test = MsTestIntegration.OnMethodBegin(testMethodInfo, testMethod.GetType(), isRetry: false);
            if (test is not null)
            {
                // Set final_status = skip for pre-execution skipped tests (ITR/attribute-based skips)
                if (test.GetTags() is { } testTags)
                {
                    testTags.FinalStatus = TestTags.StatusSkip;
                }

                test.Close(TestStatus.Skip, TimeSpan.Zero, _skipReason);
            }
        }
    }

    internal sealed class SyncImpl(Assembly assembly, string skipReason) : SkipTestMethodExecutor(assembly, skipReason)
    {
        [DuckReverseMethod(Name = "Execute", ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"])]
        public object Execute(object testMethod)
        {
            ProcessTestMethod(testMethod);
            return _arrayInstance;
        }
    }

    internal sealed class AsyncImpl(Assembly assembly, string skipReason) : SkipTestMethodExecutor(assembly, skipReason)
    {
        private object? _resultInstance;

        [DuckReverseMethod(Name = "ExecuteAsync", ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"])]
        public object Execute(object testMethod)
        {
            ProcessTestMethod(testMethod);
            _resultInstance ??= ((TaskTestResultArray?)Activator.CreateInstance(typeof(TaskTestResultArray<>).MakeGenericType([_arrayInstance.GetType()]), _arrayInstance))!.Result;
            return _resultInstance;
        }

        private abstract class TaskTestResultArray
        {
            public abstract object Result { get; }
        }

        private sealed class TaskTestResultArray<T>(T value) : TaskTestResultArray
        {
            public override object Result { get; } = Task.FromResult(value);
        }
    }
}
