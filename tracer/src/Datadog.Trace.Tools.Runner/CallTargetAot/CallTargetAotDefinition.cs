// <copyright file="CallTargetAotDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents a concrete CallTarget integration definition expanded from <see cref="InstrumentMethodAttribute"/> metadata.
/// </summary>
internal sealed class CallTargetAotDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotDefinition"/> class.
    /// </summary>
    /// <param name="targetAssemblyName">The target assembly simple name.</param>
    /// <param name="targetTypeName">The fully qualified target type name.</param>
    /// <param name="targetMethodName">The target method name.</param>
    /// <param name="returnTypeName">The expected target return type name.</param>
    /// <param name="parameterTypeNames">The expected target parameter type names.</param>
    /// <param name="integrationTypeName">The integration type full name.</param>
    /// <param name="minimumVersion">The minimum supported target assembly version.</param>
    /// <param name="maximumVersion">The maximum supported target assembly version.</param>
    /// <param name="kind">The CallTarget matching kind.</param>
    public CallTargetAotDefinition(
        string targetAssemblyName,
        string targetTypeName,
        string targetMethodName,
        string returnTypeName,
        IReadOnlyList<string> parameterTypeNames,
        string integrationTypeName,
        Version minimumVersion,
        Version maximumVersion,
        CallTargetKind kind)
    {
        TargetAssemblyName = targetAssemblyName;
        TargetTypeName = targetTypeName;
        TargetMethodName = targetMethodName;
        ReturnTypeName = returnTypeName;
        ParameterTypeNames = parameterTypeNames;
        IntegrationTypeName = integrationTypeName;
        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
        Kind = kind;
    }

    /// <summary>
    /// Gets the target assembly simple name.
    /// </summary>
    public string TargetAssemblyName { get; }

    /// <summary>
    /// Gets the fully qualified target type name.
    /// </summary>
    public string TargetTypeName { get; }

    /// <summary>
    /// Gets the target method name.
    /// </summary>
    public string TargetMethodName { get; }

    /// <summary>
    /// Gets the expected target return type name.
    /// </summary>
    public string ReturnTypeName { get; }

    /// <summary>
    /// Gets the expected target parameter type names.
    /// </summary>
    public IReadOnlyList<string> ParameterTypeNames { get; }

    /// <summary>
    /// Gets the integration type full name.
    /// </summary>
    public string IntegrationTypeName { get; }

    /// <summary>
    /// Gets the minimum supported target assembly version.
    /// </summary>
    public Version MinimumVersion { get; }

    /// <summary>
    /// Gets the maximum supported target assembly version.
    /// </summary>
    public Version MaximumVersion { get; }

    /// <summary>
    /// Gets the CallTarget matching kind.
    /// </summary>
    public CallTargetKind Kind { get; }
}
