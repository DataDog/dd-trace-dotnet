// <copyright file="CoverageBackfillDataStoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentVariablesCleaner(
    CoverageBackfillDataStore.ActualItrSkipEnvironmentVariable,
    CoverageBackfillDataStore.BackfillDataPathEnvironmentVariable)]
public class CoverageBackfillDataStoreTests
{
    [Fact]
    public void TryLoadMergesCompleteScopedActualSkipCoverage()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var firstScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var secondScope = new SkippableTestsRequestScope("Samples.NUnitTests", "scope-b");

            CoverageBackfillDataStore.Persist(testOptimization.Object, firstScope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.Persist(testOptimization.Object, secondScope, CreateCoverage("src/Calculator.cs", 0b_0100_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, firstScope);
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, secondScope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeTrue();

            coverageBackfillData.IsPresent.Should().BeTrue();
            coverageBackfillData.IsValid.Should().BeTrue();
            coverageBackfillData.ExecutedLinesByRelativePath["src/Calculator.cs"].Should().Equal([0b_1100_0000]);
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    [Fact]
    public void TryLoadFailsClosedWhenScopedActualSkipCoverageIsIncomplete()
    {
        var workspacePath = CreateWorkspacePath();
        try
        {
            var testOptimization = CreateTestOptimization(workspacePath);
            var completeScope = new SkippableTestsRequestScope("Samples.XUnitTests", "scope-a");
            var incompleteScope = new SkippableTestsRequestScope("Samples.NUnitTests", "scope-b");

            CoverageBackfillDataStore.Persist(testOptimization.Object, completeScope, CreateCoverage("src/Calculator.cs", 0b_1000_0000));
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, completeScope);
            CoverageBackfillDataStore.RecordActualItrSkip(testOptimization.Object, incompleteScope);

            CoverageBackfillDataStore.TryLoad(testOptimization.Object, out var coverageBackfillData).Should().BeFalse();

            coverageBackfillData.IsPresent.Should().BeFalse();
        }
        finally
        {
            DeleteWorkspacePath(workspacePath);
        }
    }

    private static Mock<ITestOptimization> CreateTestOptimization(string workspacePath)
    {
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.RunId).Returns("test-run");
        testOptimization.Setup(x => x.CIValues).Returns(new TestCIEnvironmentValues(workspacePath));
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor(typeof(CoverageBackfillDataStoreTests)));
        return testOptimization;
    }

    private static CoverageBackfillData CreateCoverage(string path, byte bitmap)
        => CoverageBackfillData.FromBackendCoverage(
            new Dictionary<string, string>
            {
                [path] = Convert.ToBase64String([bitmap])
            });

    private static string CreateWorkspacePath()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"dd-trace-dotnet-coverage-backfill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);
        return workspacePath;
    }

    private static void DeleteWorkspacePath(string workspacePath)
    {
        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    private sealed class TestCIEnvironmentValues : CIEnvironmentValues
    {
        public TestCIEnvironmentValues(string workspacePath)
        {
            WorkspacePath = workspacePath;
        }

        protected override void Setup(IGitInfo gitInfo)
        {
        }
    }
}
