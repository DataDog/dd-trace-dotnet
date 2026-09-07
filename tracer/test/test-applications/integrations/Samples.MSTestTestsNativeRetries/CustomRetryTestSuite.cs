// <copyright file="CustomRetryTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsNativeRetries;

#pragma warning disable MSTESTEXP // Custom retry policies are the contract exercised by this sample.

[TestClass]
[TestCategory("CustomRetry")]
public class CustomRetryTestSuite
{
    [EmptyTestMethod]
    [Retry(2)]
    public void EmptyExecutor() => Assert.Fail("The executor must not invoke this method.");

    [ThrowingTestMethod]
    [Retry(2)]
    public void ThrowingExecutor() => Assert.Fail("The executor must not invoke this method.");

    [TestMethod]
    [EmptyRetry]
    public void EmptyFinalRetry()
    {
        TestSuite.RecordAttempt(nameof(EmptyFinalRetry));
        Assert.Fail("The retry policy returns an empty final result.");
    }

    [TestMethod]
    [ThrowingRetry]
    public void ThrowingRetry()
    {
        TestSuite.RecordAttempt(nameof(ThrowingRetry));
        Assert.Fail("The retry policy throws.");
    }

    [TestMethod]
    [CanceledRetry]
    public void CanceledRetry()
    {
        TestSuite.RecordAttempt(nameof(CanceledRetry));
        Assert.Fail("The retry policy is canceled.");
    }

    private sealed class EmptyRetryAttribute : RetryBaseAttribute
    {
        protected override Task<RetryResult> ExecuteAsync(RetryContext retryContext)
        {
            var result = new RetryResult();
            result.AddResult([]);
            return Task.FromResult(result);
        }
    }

    private sealed class EmptyTestMethodAttribute : TestMethodAttribute
    {
        public EmptyTestMethodAttribute([CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
            : base(filePath, lineNumber)
        {
        }

        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) => Task.FromResult<TestResult[]>([]);
    }

    private sealed class ThrowingTestMethodAttribute : TestMethodAttribute
    {
        public ThrowingTestMethodAttribute([CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
            : base(filePath, lineNumber)
        {
        }

        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) => throw new InvalidOperationException("Custom executor failed.");
    }

    private sealed class ThrowingRetryAttribute : RetryBaseAttribute
    {
        protected override Task<RetryResult> ExecuteAsync(RetryContext retryContext)
            => throw new InvalidOperationException("Custom retry failed.");
    }

    private sealed class CanceledRetryAttribute : RetryBaseAttribute
    {
        protected override Task<RetryResult> ExecuteAsync(RetryContext retryContext)
            => throw new OperationCanceledException("Custom retry canceled.");
    }
}

#pragma warning restore MSTESTEXP
