// <copyright file="CallTargetAotGenerateCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents the command that generates the NativeAOT CallTarget artifacts.
/// </summary>
internal class CallTargetAotGenerateCommand : CommandWithExamples
{
    private readonly Option<string> _tracerAssemblyOption = new("--tracer-assembly", "Tracer assembly path used as the source of truth for future CallTarget discovery work.")
    {
        IsRequired = true
    };

    private readonly Option<string[]> _targetFolderOption = new("--target-folder", "Folder containing candidate target assemblies. Can be provided multiple times.")
    {
        IsRequired = true,
        AllowMultipleArgumentsPerToken = true
    };

    private readonly Option<string[]> _targetFilterOption = new("--target-filter", getDefaultValue: () => new[] { "*.dll" }, "Target assembly search glob. Defaults to *.dll.")
    {
        AllowMultipleArgumentsPerToken = true
    };

    private readonly Option<string?> _assemblyNameOption = new("--assembly-name", "Optional generated assembly name.");
    private readonly Option<string?> _emitPropsOption = new("--emit-props", "Optional MSBuild props output path. Defaults to <output>.props.");
    private readonly Option<string?> _emitTargetsOption = new("--emit-targets", "Optional MSBuild targets output path. Defaults to <output>.targets.");
    private readonly Option<string?> _emitTrimmerDescriptorOption = new("--emit-trimmer-descriptor", "Optional linker descriptor output path. Defaults to <output>.linker.xml.");
    private readonly Option<string?> _emitManifestOption = new("--emit-manifest", "Optional manifest output path. Defaults to <output>.manifest.json.");
    private readonly Option<string?> _emitRewritePlanOption = new("--emit-rewrite-plan", "Optional standalone rewrite-plan json path. Defaults to <output>.rewrite-plan.json.");
    private readonly Option<string?> _emitCompatReportOption = new("--emit-compat-report", "Optional compatibility report markdown path. Defaults to <output>.compat.md.");
    private readonly Option<string?> _emitCompatMatrixOption = new("--emit-compat-matrix", "Optional compatibility report json path. Defaults to <output>.compat.json.");
    private readonly Option<string> _outputOption = new("--output", "Generated AOT registry assembly output path.")
    {
        IsRequired = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotGenerateCommand"/> class.
    /// </summary>
    public CallTargetAotGenerateCommand()
        : base("generate", "Generate NativeAOT CallTarget artifacts")
    {
        AddOption(_tracerAssemblyOption);
        AddOption(_targetFolderOption);
        AddOption(_targetFilterOption);
        AddOption(_assemblyNameOption);
        AddOption(_emitPropsOption);
        AddOption(_emitTargetsOption);
        AddOption(_emitTrimmerDescriptorOption);
        AddOption(_emitManifestOption);
        AddOption(_emitRewritePlanOption);
        AddOption(_emitCompatReportOption);
        AddOption(_emitCompatMatrixOption);
        AddOption(_outputOption);

        AddExample("dd-trace calltarget-aot generate --tracer-assembly Datadog.Trace.dll --target-folder ./bin --target-filter MyApp.dll --output Datadog.Trace.CallTarget.AotRegistry.dll");
        AddExample("dd-trace calltarget-aot generate --tracer-assembly Datadog.Trace.dll --target-folder ./bin --target-folder ./refs --target-filter *.dll --output Datadog.Trace.CallTarget.AotRegistry.dll");

        this.SetHandler(Execute);
    }

    /// <summary>
    /// Executes the generation command and returns its process exit code through the invocation context.
    /// </summary>
    /// <param name="context">The command invocation context.</param>
    private void Execute(InvocationContext context)
    {
        var targetFolders = _targetFolderOption.GetValue(context) ?? Array.Empty<string>();
        var targetFilters = _targetFilterOption.GetValue(context) ?? new[] { "*.dll" };
        var tracerAssemblyPath = _tracerAssemblyOption.GetValue(context);
        var outputPath = _outputOption.GetValue(context);
        var assemblyName = _assemblyNameOption.GetValue(context);
        var propsPath = _emitPropsOption.GetValue(context) ?? (outputPath + ".props");
        var targetsPath = _emitTargetsOption.GetValue(context) ?? (outputPath + ".targets");
        var trimmerDescriptorPath = _emitTrimmerDescriptorOption.GetValue(context) ?? (outputPath + ".linker.xml");
        var manifestPath = _emitManifestOption.GetValue(context) ?? (outputPath + ".manifest.json");
        var rewritePlanPath = _emitRewritePlanOption.GetValue(context) ?? (outputPath + ".rewrite-plan.json");
        var compatibilityReportPath = _emitCompatReportOption.GetValue(context) ?? (outputPath + ".compat.md");
        var compatibilityMatrixPath = _emitCompatMatrixOption.GetValue(context) ?? (outputPath + ".compat.json");

        var options = new CallTargetAotGenerateOptions(
            tracerAssemblyPath,
            targetFolders.Where(folder => !string.IsNullOrWhiteSpace(folder)).ToList(),
            targetFilters.Where(filter => !string.IsNullOrWhiteSpace(filter)).ToList(),
            outputPath,
            assemblyName,
            targetsPath,
            propsPath,
            trimmerDescriptorPath,
            manifestPath,
            rewritePlanPath,
            compatibilityReportPath,
            compatibilityMatrixPath);

        context.ExitCode = CallTargetAotGenerateProcessor.Process(options);
    }
}
