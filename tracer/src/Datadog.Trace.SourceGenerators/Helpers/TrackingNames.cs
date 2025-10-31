// <copyright file="TrackingNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.SourceGenerators.Helpers;

internal class TrackingNames
{
    // General generator stages
    public const string PostTransform = nameof(PostTransform);
    public const string Diagnostics = nameof(Diagnostics);
    public const string ValidValues = nameof(ValidValues);
    public const string Collected = nameof(Collected);

    // Tag generator
    public const string TagResults = nameof(TagResults);
    public const string MetricResults = nameof(MetricResults);
    public const string TagDiagnostics = nameof(TagDiagnostics);
    public const string MetricDiagnostics = nameof(MetricDiagnostics);
    public const string AllTags = nameof(AllTags);
    public const string AllMetrics = nameof(AllMetrics);
    public const string AllProperties = nameof(AllProperties);

    // Call target
    public const string CallTargetDiagnostics = nameof(CallTargetDiagnostics);
    public const string AdoNetDiagnostics = nameof(AdoNetDiagnostics);
    public const string AssemblyDiagnostics = nameof(AssemblyDiagnostics);
    public const string AdoNetMergeDiagnostics = nameof(AdoNetMergeDiagnostics);
    public const string CallTargetDefinitionSource = nameof(CallTargetDefinitionSource);
    public const string AssemblyCallTargetDefinitionSource = nameof(AssemblyCallTargetDefinitionSource);
    public const string AdoNetCallTargetDefinitionSource = nameof(AdoNetCallTargetDefinitionSource);
    public const string AdoNetSignatures = nameof(AdoNetSignatures);

    // Configuration key matcher
    public const string ConfigurationKeysParseConfiguration = nameof(ConfigurationKeysParseConfiguration);
    public const string ConfigurationKeyMatcherDiagnostics = nameof(ConfigurationKeyMatcherDiagnostics);
    public const string ConfigurationKeyMatcherValidData = nameof(ConfigurationKeyMatcherValidData);
    public const string ConfigurationKeysAdditionalText = nameof(ConfigurationKeysAdditionalText);

    // Configuration key generator
    public const string ConfigurationKeysGenJsonFile = nameof(ConfigurationKeysGenJsonFile);
    public const string ConfigurationKeysGenYamlFile = nameof(ConfigurationKeysGenYamlFile);
    public const string ConfigurationKeysGenMappingFile = nameof(ConfigurationKeysGenMappingFile);
    public const string ConfigurationKeysGenMergeData = nameof(ConfigurationKeysGenMergeData);
    public const string ConfigurationKeysGenParseConfiguration = nameof(ConfigurationKeysGenParseConfiguration);
    public const string ConfigurationKeysGenParseYaml = nameof(ConfigurationKeysGenParseYaml);
    public const string ConfigurationKeysGenParseMapping = nameof(ConfigurationKeysGenParseMapping);
}
