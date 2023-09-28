// <copyright file="DatadogDiagnoser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Analysers;
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

    /// <inheritdoc />
    public IEnumerable<string> Ids { get; } = new[] { "Datadog" };

    /// <inheritdoc />
    public IEnumerable<IExporter> Exporters { get; } = new[] { DatadogExporter.Default };

    /// <inheritdoc />
    public IEnumerable<IAnalyser> Analysers { get; } = Array.Empty<IAnalyser>();

    /// <inheritdoc />
    public RunMode GetRunMode(BenchmarkCase benchmarkCase) => RunMode.NoOverhead;

    /// <inheritdoc />
    public bool RequiresBlockingAcknowledgments(BenchmarkCase benchmarkCase) => false;

    /// <inheritdoc />
    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        var utcNow = DateTime.UtcNow;
        switch (signal)
        {
            case HostSignal.BeforeAnythingElse:
                BenchmarkMetadata.SetStartTime(parameters.BenchmarkCase, utcNow);
                break;
            case HostSignal.BeforeProcessStart:
                BenchmarkMetadata.SetStartTime(parameters.BenchmarkCase.Descriptor.Type.Assembly, utcNow);
                BenchmarkMetadata.SetStartTime(parameters.BenchmarkCase.Descriptor.Type, utcNow);
                break;
            case HostSignal.AfterProcessExit:
                BenchmarkMetadata.SetEndTime(parameters.BenchmarkCase.Descriptor.Type.Assembly, utcNow);
                BenchmarkMetadata.SetEndTime(parameters.BenchmarkCase.Descriptor.Type, utcNow);
                break;
            case HostSignal.AfterAll:
                BenchmarkMetadata.SetEndTime(parameters.BenchmarkCase, utcNow);
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<Metric> ProcessResults(DiagnoserResults results) => Array.Empty<Metric>();

    /// <inheritdoc />
    public void DisplayResults(ILogger logger)
    {
    }

    /// <inheritdoc />
    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => Array.Empty<ValidationError>();
}
