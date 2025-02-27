// <copyright file="TestCaseMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal class TestCaseMetadata
{
    public TestCaseMetadata(string uniqueID, int totalExecution, int executionNumber)
    {
        TotalExecutions = totalExecution;
        ExecutionNumber = executionNumber;
        EarlyFlakeDetectionEnabled = false;
        AbortByThreshold = false;
        FlakyRetryEnabled = false;
        UniqueID = uniqueID;
    }

    public int TotalExecutions { get; set; }

    public int ExecutionNumber { get; set; }

    public int ExecutionIndex => TotalExecutions - (ExecutionNumber + 1);

    public bool EarlyFlakeDetectionEnabled { get; set; }

    public bool AbortByThreshold { get; set; }

    public bool FlakyRetryEnabled { get; set; }

    public string UniqueID { get; }
}
