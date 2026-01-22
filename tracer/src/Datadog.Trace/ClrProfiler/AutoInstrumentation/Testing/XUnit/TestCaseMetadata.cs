// <copyright file="TestCaseMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal class TestCaseMetadata
{
    public TestCaseMetadata(string uniqueID, int totalExecution, int countDownExecutionNumber)
    {
        TotalExecutions = totalExecution;
        CountDownExecutionNumber = countDownExecutionNumber;
        EarlyFlakeDetectionEnabled = false;
        AbortByThreshold = false;
        FlakyRetryEnabled = false;
        UniqueID = uniqueID;
    }

    public int TotalExecutions { get; set; }

    public int CountDownExecutionNumber { get; set; }

    public int ExecutionIndex => TotalExecutions - (CountDownExecutionNumber + 1);

    public bool EarlyFlakeDetectionEnabled { get; set; }

    public bool AbortByThreshold { get; set; }

    public bool FlakyRetryEnabled { get; set; }

    public bool IsQuarantinedTest { get; set; }

    public bool IsDisabledTest { get; set; }

    public bool IsAttemptToFix { get; set; }

    public bool IsRetry { get; set; }

    public bool IsLastRetry => IsRetry && CountDownExecutionNumber == 0;

    public bool AllAttemptsPassed { get; set; } = true;

    public bool AllRetriesFailed { get; set; } = true;

    public bool Skipped { get; set; } = false;

    public bool HasAnException { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the initial execution passed. Only PASS counts as passed, not SKIP.
    /// </summary>
    public bool InitialExecutionPassed { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether any retry execution passed. Only PASS counts as passed, not SKIP.
    /// Used for final_status calculation (distinct from AllRetriesFailed which clears on pass OR skip).
    /// </summary>
    public bool AnyRetryPassed { get; set; } = false;

    public string UniqueID { get; }
}
