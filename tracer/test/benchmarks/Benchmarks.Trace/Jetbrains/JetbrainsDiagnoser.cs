using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using JetBrains.Profiler.SelfApi;

#nullable enable

namespace Benchmarks.Trace.Jetbrains;

/// <summary>
/// Jetbrains diagnoser
/// </summary>
internal class JetbrainsDiagnoser : IDiagnoser
{
    public IEnumerable<string> Ids { get; } = new[] { "Jetbrains" };
    public IEnumerable<IExporter> Exporters { get; } = Array.Empty<IExporter>();
    public IEnumerable<IAnalyser> Analysers { get; } = Array.Empty<IAnalyser>();

    private readonly JetbrainsProduct _product;
    private readonly List<string> _filePaths;
    private readonly string? _outputFolder;

    public JetbrainsDiagnoser(JetbrainsProduct product, string? outputFolder = null)
    {
        _product = product;
        _filePaths = new List<string>();
        _outputFolder = outputFolder;
        switch (_product)
        {
            case JetbrainsProduct.Memory:
                DotMemory.EnsurePrerequisite();
                break;
            case JetbrainsProduct.Trace:
            case JetbrainsProduct.TimelineTrace:
                DotTrace.EnsurePrerequisite();
                break;
        }
    }

    public RunMode GetRunMode(BenchmarkCase benchmarkCase)
    {
        return RunMode.ExtraRun;
    }

    public bool RequiresBlockingAcknowledgments(BenchmarkCase benchmarkCase)
    {
        return false;
    }

    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        if (signal == HostSignal.BeforeActualRun)
        {
            var outputFolder = _outputFolder ?? parameters.Config.ArtifactsPath;
            var filePath = Path.Combine(outputFolder, parameters.BenchmarkCase.Descriptor.FolderInfo.Replace(".", "_") + $"_{DateTime.UtcNow:yyyy_MM_dd_HH_mm_ss}");
            switch (_product)
            {
                case JetbrainsProduct.Memory:
                    DotMemory.Attach(new DotMemory.Config().SaveToFile(filePath + ".dmw"));
                    DotMemory.GetSnapshot("Start");
                    break;
                case JetbrainsProduct.Trace:
                    DotTrace.Attach(new DotTrace.Config().SaveToFile(filePath + ".dtp"));
                    DotTrace.StartCollectingData();
                    break;
                case JetbrainsProduct.TimelineTrace:
                    DotTrace.Attach(new DotTrace.Config().SaveToFile(filePath + ".dtt").UseTimelineProfilingType(true));
                    DotTrace.StartCollectingData();
                    break;
            }
        }
        else if (signal == HostSignal.AfterActualRun)
        {
            switch (_product)
            {
                case JetbrainsProduct.Memory:
                    DotMemory.GetSnapshot("End");
                    _filePaths.Add(DotMemory.Detach());
                    break;
                case JetbrainsProduct.Trace:
                case JetbrainsProduct.TimelineTrace:
                    DotTrace.SaveData();
                    _filePaths.AddRange(DotTrace.GetCollectedSnapshotFiles());
                    DotTrace.Detach();
                    break;
            }
        }
    }

    public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
    {
        return Enumerable.Empty<Metric>();
    }

    public void DisplayResults(ILogger logger)
    {
        logger.WriteLine(LogKind.Statistic, "Jetbrains files:");
        foreach (var filePath in _filePaths)
        {
            logger.WriteLine(LogKind.Statistic, filePath);
        }
    }

    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
    {
        return Enumerable.Empty<ValidationError>();
    }
}
