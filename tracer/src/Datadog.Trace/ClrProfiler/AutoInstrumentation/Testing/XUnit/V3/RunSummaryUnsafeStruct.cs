// <copyright file="RunSummaryUnsafeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Globalization;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// This a copy of the actual Xunit.v3 RunSummary struct to copy the memory layout.
/// This is pretty unsafe and dangerous. But looking the evolution of the same struct for
/// V2, it didn't change at all. So, we are going to take the risk.
/// We validate that the target struct has the same number of fields to protect us.
/// </summary>
internal struct RunSummaryUnsafeStruct
{
    /// <summary>
    /// The total number of tests run.
    /// </summary>
    public int Total;

    /// <summary>
    /// The number of failed tests.
    /// </summary>
    public int Failed;

    /// <summary>
    /// The number of skipped tests.
    /// </summary>
    public int Skipped;

    /// <summary>
    /// The number of tests that were not run.
    /// </summary>
    public int NotRun;

    /// <summary>
    /// The total time taken to run the tests, in seconds.
    /// </summary>
    public decimal Time;

    /// <inheritdoc/>
    public readonly override string ToString()
    {
        var result = StringBuilderCache.Acquire();

        result.AppendFormat(CultureInfo.CurrentCulture, "{{ Total = {0}", Total);

        if (Failed != 0)
        {
            result.AppendFormat(CultureInfo.CurrentCulture, ", Failed = {0}", Failed);
        }

        if (Skipped != 0)
        {
            result.AppendFormat(CultureInfo.CurrentCulture, ", Skipped = {0}", Skipped);
        }

        if (NotRun != 0)
        {
            result.AppendFormat(CultureInfo.CurrentCulture, ", NotRun = {0}", NotRun);
        }

        if (Time != 0m)
        {
            result.AppendFormat(CultureInfo.CurrentCulture, ", Time = {0}", Time);
        }

        result.Append(" }");
        return StringBuilderCache.GetStringAndRelease(result);
    }
}
