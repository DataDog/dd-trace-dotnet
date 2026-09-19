// <copyright file="SkipTestMethodExecutor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal abstract class SkipTestMethodExecutor
{
    private const string TestMethodAttributeTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute";
    private const string TestMethodTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod";
    private const string TestResultTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestResult";
    private static readonly ConditionalWeakTable<Type, ExecutorMetadata> ExecutorMetadataCache = new();

    private readonly object _arrayInstance;
    private readonly string _skipReason;
    private readonly bool _recordCoverageBackfillSkip;
    private readonly SkippableTest? _skippableTest;

    protected SkipTestMethodExecutor(Type executorType, string skipReason, bool recordCoverageBackfillSkip = false, SkippableTest? skippableTest = null)
    {
        var metadata = GetExecutorMetadata(executorType);
        TestMethodAttributeType = metadata.TestMethodAttributeType;
        var testResultType = metadata.TestResultType;
        var array = Array.CreateInstance(testResultType, 1);
        var result = Activator.CreateInstance(testResultType);
        if (DuckType.Create<ITestResult>(result) is { } iResult)
        {
            iResult.Outcome = UnitTestOutcome.Inconclusive; // Inconclusive is reported as Skipped in the CLI
        }

        array.SetValue(result, 0);
        _arrayInstance = array;
        _skipReason = skipReason;
        _recordCoverageBackfillSkip = recordCoverageBackfillSkip;
        _skippableTest = skippableTest;
    }

    internal Type TestMethodAttributeType { get; }

    internal static SkipTestMethodExecutor Create(Type executorType, string skipReason, bool recordCoverageBackfillSkip = false, SkippableTest? skippableTest = null)
    {
        return GetExecutorMetadata(executorType).UseAsyncExecutor
                   ? new AsyncImpl(executorType, skipReason, recordCoverageBackfillSkip, skippableTest)
                   : new SyncImpl(executorType, skipReason, recordCoverageBackfillSkip, skippableTest);
    }

    internal static bool IsReplacement(object? executor)
        => executor is SkipTestMethodExecutor ||
           executor is IDuckType { Instance: SkipTestMethodExecutor };

    private static ExecutorMetadata GetExecutorMetadata(Type executorType)
        => ExecutorMetadataCache.GetValue(executorType, static type => FindExecutorMetadata(type));

    private static ExecutorMetadata FindExecutorMetadata(Type executorType)
    {
        var currentType = executorType;
        while (currentType is not null)
        {
            if (currentType.FullName == TestMethodAttributeTypeName &&
                currentType.Assembly.GetType(TestResultTypeName, throwOnError: false) is { } testResultType)
            {
                var testMethodType = currentType.Assembly.GetType(TestMethodTypeName, throwOnError: true)!;

                // MSTest 3.9-3.11 has an internal ExecuteAsync that dispatches to public Execute.
                // MSTest 4 exposes ExecuteAsync publicly, so select only a method the proxy can override.
                var publicExecuteAsync = currentType.GetMethod(
                    "ExecuteAsync",
                    BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: [testMethodType],
                    modifiers: null);
                return new ExecutorMetadata(currentType, testResultType, publicExecuteAsync is not null);
            }

            currentType = currentType.BaseType;
        }

        throw new TypeLoadException($"Could not find '{TestMethodAttributeTypeName}' in the type hierarchy of '{executorType.FullName}'.");
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
                test.GetTags().FinalStatus = TestTags.StatusSkip;
                test.Close(TestStatus.Skip, TimeSpan.Zero, _skipReason);
            }

            if (_recordCoverageBackfillSkip)
            {
                if (_skippableTest is { } skippableTest)
                {
                    MsTestIntegration.RecordTestSkipCoverageBackfill(testMethodInfo, skippableTest);
                }
                else
                {
                    MsTestIntegration.RecordTestSkipCoverageBackfill(testMethodInfo);
                }
            }
        }
    }

    internal sealed class SyncImpl(Type executorType, string skipReason, bool recordCoverageBackfillSkip = false, SkippableTest? skippableTest = null)
        : SkipTestMethodExecutor(executorType, skipReason, recordCoverageBackfillSkip, skippableTest)
    {
        [DuckReverseMethod(Name = "Execute", ParameterTypeNames = ["Microsoft.VisualStudio.TestTools.UnitTesting.ITestMethod"])]
        public object Execute(object testMethod)
        {
            ProcessTestMethod(testMethod);
            return _arrayInstance;
        }
    }

    internal sealed class AsyncImpl(Type executorType, string skipReason, bool recordCoverageBackfillSkip = false, SkippableTest? skippableTest = null)
        : SkipTestMethodExecutor(executorType, skipReason, recordCoverageBackfillSkip, skippableTest)
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

    private sealed class ExecutorMetadata(Type testMethodAttributeType, Type testResultType, bool useAsyncExecutor)
    {
        public Type TestMethodAttributeType { get; } = testMethodAttributeType;

        public Type TestResultType { get; } = testResultType;

        public bool UseAsyncExecutor { get; } = useAsyncExecutor;
    }
}
