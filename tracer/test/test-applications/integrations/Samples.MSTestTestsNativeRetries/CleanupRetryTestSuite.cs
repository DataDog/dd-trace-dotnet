// <copyright file="CleanupRetryTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsNativeRetries;

[TestClass]
[TestCategory("CustomRetry")]
public class CleanupRetryTestSuite
{
    [TestMethod]
    [Retry(2)]
    public void PassesBeforeCleanupFailure()
        => Assert.IsTrue(TestSuite.RecordAttempt(nameof(PassesBeforeCleanupFailure)) >= 2);

    [ClassCleanup]
    public static void Cleanup() => throw new InvalidOperationException("Class cleanup failed after the retry.");
}
