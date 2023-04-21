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
    private List<string> _filePaths;
    private string? _outputFolder;

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
            var filePath = Path.Combine(outputFolder, parameters.BenchmarkCase.Descriptor.FolderInfo.Replace(".", "_") + $"_{DateTime.UtcNow.ToBinary()}");
            switch (_product)
            {
                case JetbrainsProduct.Memory:
                    DotMemory.Attach(new DotMemory.Config().SaveToFile(filePath));
                    DotMemory.GetSnapshot("Start");
                    break;
                case JetbrainsProduct.Trace:
                    DotTrace.Attach(new DotTrace.Config().SaveToFile(filePath));
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
