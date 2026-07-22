// <copyright file="GlobalCoverageMemoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace Samples.NUnitGlobalCoverageMemory;

[TestFixture]
[NonParallelizable]
public class GlobalCoverageMemoryTests
{
    private const int DefaultCaseCount = 6_000;
    private const string CaseCountEnvironmentVariable = "NUNIT_GLOBAL_COVERAGE_CASE_COUNT";
    private const string ProgressPathEnvironmentVariable = "NUNIT_GLOBAL_COVERAGE_PROGRESS_PATH";
    private static int _completed;

    public static IEnumerable Cases()
    {
        var configuredCount = Environment.GetEnvironmentVariable(CaseCountEnvironmentVariable);
        var count = string.IsNullOrEmpty(configuredCount) ? DefaultCaseCount : ParseSmokeCaseCount(configuredCount);
        for (var i = 0; i < count; i++)
        {
            yield return new TestCaseData(i).SetName($"GlobalCoverageMemory_{i:D4}");
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void ExecutesSparseInstrumentedLine(int value)
    {
        Assert.That(CoveredMethod(value), Is.EqualTo(value + 1));

        var completed = Interlocked.Increment(ref _completed);
        if (completed % 100 == 0 || completed == 1)
        {
            WriteProgress(completed);
        }
    }

    private static int CoveredMethod(int value)
    {
#line 131072
        return value + 1;
#line default
    }

    private static int ParseSmokeCaseCount(string value)
    {
        if (value == "1")
        {
            return 1;
        }

        throw new InvalidOperationException($"{CaseCountEnvironmentVariable} only accepts the test-only value '1'.");
    }

    private static void WriteProgress(int completed)
    {
        var path = Environment.GetEnvironmentVariable(ProgressPathEnvironmentVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        var record = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"pid\":{0},\"completed\":{1},\"privateBytes\":{2},\"managedBytes\":{3},\"timestamp\":\"{4:O}\"}}{5}",
            process.Id,
            completed,
            process.PrivateMemorySize64,
            GC.GetTotalMemory(forceFullCollection: false),
            DateTimeOffset.UtcNow,
            Environment.NewLine);

        File.AppendAllText(path, record);
    }
}
