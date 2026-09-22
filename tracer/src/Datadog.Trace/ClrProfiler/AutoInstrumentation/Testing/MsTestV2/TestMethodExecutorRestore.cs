// <copyright file="TestMethodExecutorRestore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal static class TestMethodExecutorRestore
{
    internal static CallTargetState Create(ITestMethodOptions testMethodOptions, object originalExecutor, object replacementExecutor)
        => new(null, new TestMethodOptionsExecutorRestoreState(testMethodOptions, originalExecutor, replacementExecutor));

    internal static CallTargetState Create(ITestMethodInfoV3_9 testMethodInfo, object originalExecutor, object replacementExecutor)
        => new(null, new TestMethodInfoExecutorRestoreState(testMethodInfo, originalExecutor, replacementExecutor));

    internal static void Restore(in CallTargetState state)
    {
        if (state.State is ExecutorRestoreState restoreState)
        {
            restoreState.Restore();
        }
    }

    private abstract class ExecutorRestoreState
    {
        private readonly object _originalExecutor;
        private readonly object _replacementExecutor;

        protected ExecutorRestoreState(object originalExecutor, object replacementExecutor)
        {
            _originalExecutor = originalExecutor;
            _replacementExecutor = replacementExecutor;
        }

        protected abstract object? CurrentExecutor { get; set; }

        internal void Restore()
        {
            if (ReferenceEquals(CurrentExecutor, _replacementExecutor))
            {
                CurrentExecutor = _originalExecutor;
            }
        }
    }

    private sealed class TestMethodOptionsExecutorRestoreState : ExecutorRestoreState
    {
        private readonly ITestMethodOptions _testMethodOptions;

        public TestMethodOptionsExecutorRestoreState(ITestMethodOptions testMethodOptions, object originalExecutor, object replacementExecutor)
            : base(originalExecutor, replacementExecutor)
        {
            _testMethodOptions = testMethodOptions;
        }

        protected override object? CurrentExecutor
        {
            get => _testMethodOptions.Executor;
            set => _testMethodOptions.Executor = value;
        }
    }

    private sealed class TestMethodInfoExecutorRestoreState : ExecutorRestoreState
    {
        private readonly ITestMethodInfoV3_9 _testMethodInfo;

        public TestMethodInfoExecutorRestoreState(ITestMethodInfoV3_9 testMethodInfo, object originalExecutor, object replacementExecutor)
            : base(originalExecutor, replacementExecutor)
        {
            _testMethodInfo = testMethodInfo;
        }

        protected override object? CurrentExecutor
        {
            get => _testMethodInfo.Executor;
            set => _testMethodInfo.Executor = value;
        }
    }
}
