// <copyright file="DatadogDiagnoser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog BenchmarkDotNet Diagnoser
/// </summary>
public class DatadogDiagnoser : IDiagnoser
{
    /// <summary>
    /// Default DatadogDiagnoser instance
    /// </summary>
    public static readonly IDiagnoser Default = new DatadogDiagnoser();

    private DateTimeOffset? _startSessionDateTimeOffset;
    private DateTimeOffset? _endSessionDateTimeOffset;
    private DateTimeOffset? _startRunDateTimeOffset;
    private DateTimeOffset? _endRunDateTimeOffset;

    /// <inheritdoc />
    public IEnumerable<string> Ids { get; } = new[] { "Datadog" };

    /// <inheritdoc />
    public IEnumerable<IExporter> Exporters { get; } = new[] { DatadogExporter.Default };

    /// <inheritdoc />
    public IEnumerable<IAnalyser> Analysers { get; } = Array.Empty<IAnalyser>();

    /// <inheritdoc />
    public RunMode GetRunMode(BenchmarkCase benchmarkCase)
    {
        return RunMode.NoOverhead;
    }

    /// <inheritdoc />
    public bool RequiresBlockingAcknowledgments(BenchmarkCase benchmarkCase)
    {
        return false;
    }

    /// <inheritdoc />
    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        if (signal == HostSignal.BeforeProcessStart)
        {
            _startSessionDateTimeOffset = DateTimeOffset.UtcNow;
        }
        else if (signal == HostSignal.BeforeActualRun)
        {
            _startRunDateTimeOffset = DateTimeOffset.UtcNow;
        }
        else if (signal == HostSignal.AfterActualRun)
        {
            _endRunDateTimeOffset = DateTimeOffset.UtcNow;
        }
        else if (signal == HostSignal.AfterProcessExit)
        {
            _endSessionDateTimeOffset = DateTimeOffset.UtcNow;
        }
    }

    /// <inheritdoc />
    public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
    {
        yield return new Metric(new DateTimeOffSetMetricDescriptor { Id = "StartSessionDate" }, _startSessionDateTimeOffset?.Ticks * TimeConstants.NanoSecondsPerTick ?? 0d);
        yield return new Metric(new DateTimeOffSetMetricDescriptor { Id = "StartDate" }, _startRunDateTimeOffset?.Ticks * TimeConstants.NanoSecondsPerTick ?? 0d);
        yield return new Metric(new DateTimeOffSetMetricDescriptor { Id = "EndDate" }, _endRunDateTimeOffset?.Ticks * TimeConstants.NanoSecondsPerTick ?? 0d);
        yield return new Metric(new DateTimeOffSetMetricDescriptor { Id = "EndSessionDate" }, _endSessionDateTimeOffset?.Ticks * TimeConstants.NanoSecondsPerTick ?? 0d);
    }

    /// <inheritdoc />
    public void DisplayResults(ILogger logger)
    {
    }

    /// <inheritdoc />
    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
    {
        yield break;
    }

    private class DateTimeOffSetMetricDescriptor : IMetricDescriptor
    {
        public string Id { get; set; }

        public string DisplayName => Id;

        public string Legend => Id;

        public string NumberFormat => "0";

        public UnitType UnitType => UnitType.Time;

        public string Unit => "ns";

        public bool TheGreaterTheBetter => false;

        public int PriorityInCategory => 0;
    }
}
