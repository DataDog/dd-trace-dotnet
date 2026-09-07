// <copyright file="TestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]

namespace Samples.MSTestTestsNativeRetries;

[TestClass]
public class TestSuite
{
    private static readonly Dictionary<string, int> Attempts = new();

    public TestContext TestContext { get; set; }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [TestCategory("CustomRetry")]
    [DataRow(0, DisplayName = "Mixed row")]
    [DataRow(1, DisplayName = "Mixed row")]
    [Retry(2)]
    public void MixedRowOutcomes(int row)
    {
        var attempt = RecordAttempt(nameof(MixedRowOutcomes) + row);
        if (row == 0)
        {
            Assert.Inconclusive("This row remains inconclusive while the other row is retried.");
        }

        Assert.IsTrue(attempt >= 2);
    }

    [TestMethod]
    [TestCategory("CustomRetry")]
    [Timeout(100, CooperativeCancellation = true)]
    [Retry(2)]
    public async Task TimesOut()
    {
        RecordAttempt(nameof(TimesOut));
        await Task.Delay(1_000, TestContext.CancellationToken);
    }

    [TestMethod]
    [Retry(2)]
    public void PassesImmediately() => RecordAttempt(nameof(PassesImmediately));

    [TestMethod]
    [Retry(2)]
    public void PassesOnThirdAttempt() => Assert.IsTrue(RecordAttempt(nameof(PassesOnThirdAttempt)) >= 3);

    [TestMethod]
    [Retry(2)]
    public void AlwaysFails()
    {
        RecordAttempt(nameof(AlwaysFails));
        Assert.Fail("Every attempt fails.");
    }

    [TestMethod]
    [Retry(2)]
    public async Task PassesAfterAwait()
    {
        await Task.Yield();
        Assert.IsTrue(RecordAttempt(nameof(PassesAfterAwait)) >= 2);
    }

    [TestMethod]
    [Retry(2)]
    public void BecomesInconclusive()
    {
        if (RecordAttempt(nameof(BecomesInconclusive)) == 1)
        {
            Assert.Fail("The first attempt fails.");
        }

        Assert.Inconclusive("The retry is inconclusive.");
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Same row name")]
    [DataRow(1, DisplayName = "Same row name")]
    [Retry(2)]
    public void ParameterizedRetry(int row)
    {
        Assert.IsTrue(RecordAttempt(nameof(ParameterizedRetry) + row) > row);
    }

    internal static int RecordAttempt(string name)
    {
        lock (Attempts)
        {
            Attempts.TryGetValue(name, out var attempt);
            Attempts[name] = ++attempt;
            var outputPath = Environment.GetEnvironmentVariable("MSTEST_ATTEMPTS_FILE");
            if (!string.IsNullOrEmpty(outputPath))
            {
                File.AppendAllText(outputPath, name + ":" + attempt + Environment.NewLine);
            }

            return attempt;
        }
    }
}
