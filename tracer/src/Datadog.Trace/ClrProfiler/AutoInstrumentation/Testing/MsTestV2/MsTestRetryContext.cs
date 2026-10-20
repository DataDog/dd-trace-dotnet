// <copyright file="MsTestRetryContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// Preserves a row's inputs while MSTest completes its native retry policy.
/// Each Datadog retry gets a fresh TestContext; InvokeAsync creates the test class instance.
/// </summary>
internal sealed class MsTestRetryContext : IDisposable
{
    private readonly ITestMethod _testMethod;
    private readonly ITestMethodInfoWithRetry _methodInfo;
    private readonly IMsTestContext _originalContext;
    private readonly ExecutionContext? _executionContext;
    private readonly object[]? _arguments;
    private readonly string? _displayName;
    private int _testRunCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsTestRetryContext"/> class before the test span is activated.
    /// The array copy survives MSTest clearing arguments; argument objects retain their original identity.
    /// </summary>
    public MsTestRetryContext(ITestMethod testMethod)
    {
        _testMethod = testMethod;
        _methodInfo = testMethod.Instance.DuckCast<ITestMethodInfoWithRetry>()!;
        _originalContext = _methodInfo.TestContext.DuckCast<IMsTestContext>();
        _arguments = (object[]?)testMethod.Arguments?.Clone();
        _displayName = _originalContext.TestDisplayName;
        _testRunCount = _originalContext.TestRunCount;
        // Capture before creating the test span, so retries do not inherit a pending attempt's scope.
        _executionContext = ExecutionContext.Capture();
    }

    /// <summary>
    /// Runs a retry in a copy of the captured context, without inheriting a pending attempt's span.
    /// </summary>
    public Task<T> RunAsync<T>(Func<Task<T>> retry)
    {
        if (_executionContext is null)
        {
            return RunWithTestContextAsync(retry);
        }

        Task<T>? task = null;
        using var context = _executionContext.CreateCopy();
        ExecutionContext.Run(context, _ => task = RunWithTestContextAsync(retry), null);
        return task!;
    }

    /// <summary>
    /// Releases the captured ExecutionContext when the owning runner finishes all attempts.
    /// </summary>
    public void Dispose() => _executionContext?.Dispose();

    /// <summary>
    /// Installs a fresh TestContext and restores the original arguments and context on every exit.
    /// The supplied retry invokes MSTest InvokeAsync, which creates and cleans up a fresh test class.
    /// </summary>
    private async Task<T> RunWithTestContextAsync<T>(Func<Task<T>> retry)
    {
        var previousArguments = _testMethod.Arguments;
        var previousContext = _methodInfo.TestContext;
        var context = _originalContext.CloneForDataDrivenIteration().DuckCast<IMsTestContext>();
        try
        {
            _methodInfo.SetArguments(_arguments);
            context.SetTestData(_arguments);
            context.SetDisplayName(_displayName);
            // The policy can select an older attempt, but retries follow every native execution.
            _testRunCount = Math.Max(_testRunCount, previousContext.DuckCast<IMsTestContext>().TestRunCount) + 1;
            context.TestRunCount = _testRunCount;
            _methodInfo.TestContext = context.Instance!;
            using (context.SetCurrentTestContext(context.Instance!))
            {
                return await retry().ConfigureAwait(false);
            }
        }
        finally
        {
            _methodInfo.SetArguments(previousArguments);
            _methodInfo.TestContext = previousContext;
            context.Dispose();
        }
    }
}
