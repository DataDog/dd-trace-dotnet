// <copyright file="AotInstrumentCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

namespace Datadog.Trace.Tools.Runner.Aot;

/// <summary>
/// <c>dd-trace aot instrument</c>: rewrites assemblies with the native tracer hosted offline.
/// </summary>
internal class AotInstrumentCommand : CommandWithExamples
{
    private readonly Option<string> _nativeTracerOption = new("--native-tracer", "Path to the Datadog.Tracer.Native library for the build host.") { IsRequired = true };
    private readonly Option<string> _datadogTraceOption = new("--datadog-trace", "Path to the Datadog.Trace.dll compiled into the application (same version as the native tracer).") { IsRequired = true };
    private readonly Option<string[]> _assemblyOption = new("--assembly", "Assembly to instrument. Can be provided multiple times.") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
    private readonly Option<string[]> _referenceDirOption = new("--reference-dir", "Directory used to resolve references (must contain System.Private.CoreLib.dll). Can be provided multiple times.") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
    private readonly Option<string> _outputOption = new("--output", "Output directory for the instrumented assemblies.") { IsRequired = true };
    private readonly Option<string> _runtimeVersionOption = new("--runtime-version", getDefaultValue: () => "8.0.0", "Version of the .NET runtime the application targets.");
    private readonly Option<string> _categoriesOption = new("--categories", getDefaultValue: () => "tracing,appsec,rasp", "Instrumentation categories: tracing, appsec, rasp, iast.");
    private readonly Option<bool> _noEmbeddedDefinitionsOption = new("--no-embedded-definitions", "Do not enable the definitions embedded in the native tracer.") { IsHidden = true };
    private readonly Option<string?> _definitionsAssemblyOption = new("--definitions-assembly", "Assembly with a method that registers extra definitions through Datadog.Trace (tests).") { IsHidden = true };
    private readonly Option<string?> _definitionsMethodOption = new("--definitions-method", "Type::Method that registers the extra definitions (tests).") { IsHidden = true };
    private readonly Option<string[]> _neutralizeOption = new("--neutralize", "Type::Method whose body is replaced by a return (tests).") { IsHidden = true, AllowMultipleArgumentsPerToken = true };
    private readonly Option<bool> _noCallTargetRegistryOption = new("--no-calltarget-registry", "Do not generate the CallTarget registrations (to compare the native rewrite alone).") { IsHidden = true };
    private readonly Option<string[]> _duckTypeMapOption = new("--ducktype-map", "ducktype-aot map file recorded at runtime (DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH) with the duck typing mappings to serve too. Can be provided multiple times.") { AllowMultipleArgumentsPerToken = true };
    private readonly Option<string?> _reportOption = new("--report", "Optional JSON report path.");
    private readonly Option<bool> _verifyOption = new("--verify", "Verify the rewritten methods (ILSpy, JIT preparation, ILVerify) against the original assemblies.");
    private readonly Option<bool> _verboseOption = new("--verbose", "Verbose output.");

    public AotInstrumentCommand()
        : base("instrument", "Instrument assemblies at build time with the native tracer")
    {
        AddOption(_nativeTracerOption);
        AddOption(_datadogTraceOption);
        AddOption(_assemblyOption);
        AddOption(_referenceDirOption);
        AddOption(_outputOption);
        AddOption(_runtimeVersionOption);
        AddOption(_categoriesOption);
        AddOption(_noEmbeddedDefinitionsOption);
        AddOption(_definitionsAssemblyOption);
        AddOption(_definitionsMethodOption);
        AddOption(_neutralizeOption);
        AddOption(_noCallTargetRegistryOption);
        AddOption(_duckTypeMapOption);
        AddOption(_reportOption);
        AddOption(_verifyOption);
        AddOption(_verboseOption);

        this.SetHandler(Execute);
    }

    internal static uint ParseCategories(string value)
    {
        uint categories = 0;
        foreach (var category in value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            categories |= category.Trim().ToLowerInvariant() switch
            {
                "tracing" => 1u,
                "appsec" => 2u,
                "iast" => 4u,
                "rasp" => 8u,
                _ => throw new ArgumentException($"Unknown instrumentation category '{category}'."),
            };
        }

        return categories;
    }

    private void Execute(InvocationContext context)
    {
        var options = new AotInstrumentOptions
        {
            NativeTracerPath = _nativeTracerOption.GetValue(context),
            DatadogTracePath = _datadogTraceOption.GetValue(context),
            Assemblies = (_assemblyOption.GetValue(context) ?? Array.Empty<string>()).ToList(),
            ReferenceDirectories = (_referenceDirOption.GetValue(context) ?? Array.Empty<string>()).ToList(),
            OutputDirectory = _outputOption.GetValue(context),
            RuntimeVersion = Version.Parse(_runtimeVersionOption.GetValue(context)),
            Categories = ParseCategories(_categoriesOption.GetValue(context)),
            UseEmbeddedDefinitions = !_noEmbeddedDefinitionsOption.GetValue(context),
            DefinitionsAssembly = _definitionsAssemblyOption.GetValue(context),
            DefinitionsMethod = _definitionsMethodOption.GetValue(context),
            Neutralize = (_neutralizeOption.GetValue(context) ?? Array.Empty<string>()).ToList(),
            GenerateCallTargetRegistry = !_noCallTargetRegistryOption.GetValue(context),
            DuckTypeMaps = (_duckTypeMapOption.GetValue(context) ?? Array.Empty<string>()).ToList(),
            ReportPath = _reportOption.GetValue(context),
            Verify = _verifyOption.GetValue(context),
            Verbose = _verboseOption.GetValue(context),
        };

        context.ExitCode = AotInstrumentProcessor.Process(options);
    }
}
