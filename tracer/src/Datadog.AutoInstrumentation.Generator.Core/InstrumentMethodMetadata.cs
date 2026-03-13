// <copyright file="InstrumentMethodMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.AutoInstrumentation.Generator.Core;

/// <summary>
/// Structured metadata about the generated [InstrumentMethod] attribute values.
/// </summary>
public class InstrumentMethodMetadata
{
    public string AssemblyName { get; set; } = string.Empty;

    public string TypeName { get; set; } = string.Empty;

    public string MethodName { get; set; } = string.Empty;

    public string ReturnTypeName { get; set; } = string.Empty;

    public string ParameterTypeNames { get; set; } = string.Empty;

    public string MinimumVersion { get; set; } = string.Empty;

    public string MaximumVersion { get; set; } = string.Empty;

    public string IntegrationName { get; set; } = string.Empty;

    public string IntegrationClassName { get; set; } = string.Empty;

    public bool IsInterface { get; set; }
}
