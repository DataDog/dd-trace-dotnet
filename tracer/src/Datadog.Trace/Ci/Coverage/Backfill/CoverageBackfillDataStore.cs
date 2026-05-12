// <copyright file="CoverageBackfillDataStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Persists backend ITR coverage data and actual-skip state so coverage adapters running in helper domains or child tool processes can backfill safely.
/// </summary>
internal static class CoverageBackfillDataStore
{
    /// <summary>
    /// Environment variable that points to the persisted backend coverage map for this test-optimization run.
    /// </summary>
    public const string BackfillDataPathEnvironmentVariable = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_PATH";

    /// <summary>
    /// Environment variable set after the process observes at least one real ITR skip.
    /// </summary>
    public const string ActualItrSkipEnvironmentVariable = "DD_CIVISIBILITY_ITR_COVERAGE_BACKFILL_ACTUAL_SKIP";

    private const string BackfillFileName = "coverage-backfill.json";

    /// <summary>
    /// Persists backend coverage data for later coverage adapters and propagates the file path through the process environment.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance that owns the run id and workspace.</param>
    /// <param name="coverageBackfillData">Decoded backend coverage data returned by the skippable-tests endpoint.</param>
    public static void Persist(ITestOptimization testOptimization, CoverageBackfillData coverageBackfillData)
    {
        if (coverageBackfillData is not { IsPresent: true, IsValid: true })
        {
            return;
        }

        try
        {
            var baseDirectory = testOptimization.CIValues.WorkspacePath ?? Environment.CurrentDirectory;
            var folder = Path.Combine(baseDirectory, ".dd", testOptimization.RunId);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var filePath = Path.Combine(folder, BackfillFileName);
            File.WriteAllText(filePath, JsonHelper.SerializeObject(coverageBackfillData));
            EnvironmentHelpers.SetEnvironmentVariable(BackfillDataPathEnvironmentVariable, filePath);
        }
        catch (Exception ex)
        {
            testOptimization.Log.Warning(ex, "CoverageBackfillDataStore: Error persisting ITR coverage backfill data.");
        }
    }

    /// <summary>
    /// Loads persisted backend coverage data from the path propagated in the environment.
    /// </summary>
    /// <param name="coverageBackfillData">Decoded backend coverage data when the persisted file is available and valid.</param>
    /// <returns>True when valid backend coverage data was loaded.</returns>
    public static bool TryLoad(out CoverageBackfillData coverageBackfillData)
    {
        coverageBackfillData = CoverageBackfillData.Missing;
// TODO temporary, this needs to be addressed
#pragma warning disable DD0012
        var filePath = EnvironmentHelpers.GetEnvironmentVariable(BackfillDataPathEnvironmentVariable);
#pragma warning restore DD0012
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var data = JsonHelper.DeserializeObject<CoverageBackfillData>(File.ReadAllText(filePath));
            if (data is { IsPresent: true, IsValid: true })
            {
                coverageBackfillData = data;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Records in the process environment that a test was actually skipped by ITR.
    /// </summary>
    public static void RecordActualItrSkip()
    {
        EnvironmentHelpers.SetEnvironmentVariable(ActualItrSkipEnvironmentVariable, "1");
    }

    /// <summary>
    /// Gets whether the current process environment has observed an actual ITR skip.
    /// </summary>
    /// <returns>True when a prior test closed with the ITR skip reason in this process.</returns>
    public static bool HasActualItrSkip()
    {
// TODO temporary, this needs to be addressed
#pragma warning disable DD0012
        var actualItrSkip = EnvironmentHelpers.GetEnvironmentVariable(ActualItrSkipEnvironmentVariable);
#pragma warning restore DD0012
        return string.Equals(actualItrSkip, "1", StringComparison.Ordinal);
    }
}
