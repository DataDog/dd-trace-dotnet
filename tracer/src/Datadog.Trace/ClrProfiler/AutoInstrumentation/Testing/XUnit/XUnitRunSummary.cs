// <copyright file="XUnitRunSummary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal enum XUnitFrameworkResult
{
    Unknown,
    Passed,
    Failed,
    Skipped,
    NotRun,
}

internal struct XUnitRunSummary
{
    private XUnitFrameworkResult _frameworkResult;

    public int Total;

    public int Failed;

    public int Skipped;

    public int NotRun;

    public decimal Time;

    public void Aggregate(in XUnitRunSummary other)
    {
        var frameworkResult = GetFrameworkResult();
        var otherFrameworkResult = GetFrameworkResult(in other);

        Total += other.Total;
        Failed += other.Failed;
        Skipped += other.Skipped;
        NotRun += other.NotRun;
        Time += other.Time;

        // Match RetryMessageBus: the first passing execution wins; otherwise keep the first execution.
        if (frameworkResult == XUnitFrameworkResult.Unknown || otherFrameworkResult == XUnitFrameworkResult.Passed)
        {
            _frameworkResult = otherFrameworkResult;
        }
    }

    public void HideQuarantinedOrDisabledResult()
    {
        Total = 1;
        Failed = 0;
        Skipped = 0;
        NotRun = 0;
        _frameworkResult = XUnitFrameworkResult.Passed;
    }

    public void ReportQuarantinedOrDisabledResultAsSkipped()
    {
        Total = 1;
        Failed = 0;
        Skipped = 1;
        NotRun = 0;
        _frameworkResult = XUnitFrameworkResult.Skipped;
    }

    public void NormalizeFrameworkResult()
    {
        var frameworkResult = GetFrameworkResult();
        Total = 1;
        Failed = frameworkResult == XUnitFrameworkResult.Failed ? 1 : 0;
        Skipped = frameworkResult == XUnitFrameworkResult.Skipped ? 1 : 0;
        NotRun = frameworkResult == XUnitFrameworkResult.NotRun ? 1 : 0;
        _frameworkResult = frameworkResult;
    }

    public XUnitFrameworkResult GetFrameworkResult()
    {
        if (_frameworkResult == XUnitFrameworkResult.Unknown)
        {
            _frameworkResult = GetFrameworkResult(in this);
        }

        return _frameworkResult;
    }

    private static XUnitFrameworkResult GetFrameworkResult(in XUnitRunSummary summary)
    {
        if (summary._frameworkResult != XUnitFrameworkResult.Unknown)
        {
            return summary._frameworkResult;
        }

        if (summary.Total - summary.Skipped - summary.Failed - summary.NotRun > 0)
        {
            return XUnitFrameworkResult.Passed;
        }

        if (summary.Skipped > 0)
        {
            return XUnitFrameworkResult.Skipped;
        }

        if (summary.Failed > 0)
        {
            return XUnitFrameworkResult.Failed;
        }

        return summary.NotRun > 0 ? XUnitFrameworkResult.NotRun : XUnitFrameworkResult.Unknown;
    }
}
