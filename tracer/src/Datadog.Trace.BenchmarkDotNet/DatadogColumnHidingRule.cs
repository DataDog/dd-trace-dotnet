// <copyright file="DatadogColumnHidingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using BenchmarkDotNet.Columns;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog column hiding rule
/// </summary>
public class DatadogColumnHidingRule : IColumnHidingRule
{
    /// <summary>
    /// Gets the default instance
    /// </summary>
    public static IColumnHidingRule Default { get; } = new DatadogColumnHidingRule();

    /// <inheritdoc />
    public bool NeedToHide(IColumn column)
    {
        if (column.Id is "StartDate" or "EndDate")
        {
            return true;
        }

        return false;
    }
}
