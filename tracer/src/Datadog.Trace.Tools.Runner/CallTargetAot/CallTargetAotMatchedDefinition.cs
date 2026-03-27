// <copyright file="CallTargetAotMatchedDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Represents a concrete target method match selected for CallTarget NativeAOT generation.
/// </summary>
internal sealed class CallTargetAotMatchedDefinition
{
    /// <summary>
    /// Gets or sets a value indicating whether the matched binding can be emitted by the current AOT adapter set.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// Gets or sets the compatibility status recorded for the binding.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the diagnostic code recorded for unsupported bindings.
    /// </summary>
    public string? DiagnosticCode { get; set; }

    /// <summary>
    /// Gets or sets the diagnostic message recorded for unsupported bindings.
    /// </summary>
    public string? DiagnosticMessage { get; set; }

    /// <summary>
    /// Gets or sets the target assembly simple name.
    /// </summary>
    public string TargetAssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute path to the matched target assembly.
    /// </summary>
    public string TargetAssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified target type name.
    /// </summary>
    public string TargetTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target method name.
    /// </summary>
    public string TargetMethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the matched target return type name.
    /// </summary>
    public string ReturnTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the matched target parameter type names.
    /// </summary>
    public List<string> ParameterTypeNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the integration type full name.
    /// </summary>
    public string IntegrationTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of target method arguments that the begin handler receives.
    /// </summary>
    public int BeginArgumentCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the matched target method must use the slow-begin object-array path.
    /// </summary>
    public bool UsesSlowBegin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the matched target method returns a value.
    /// </summary>
    public bool ReturnsValue { get; set; }

    /// <summary>
    /// Gets or sets the handler kind emitted for the match.
    /// </summary>
    public string HandlerKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the matched target method requires an async continuation callback.
    /// </summary>
    public bool RequiresAsyncContinuation { get; set; }

    /// <summary>
    /// Gets or sets the async continuation result type name when the target method returns Task{TResult} or ValueTask{TResult}.
    /// </summary>
    public string? AsyncResultTypeName { get; set; }

    /// <summary>
    /// Gets or sets the canonical DuckType mapping key used to proxy the target instance before invoking the integration.
    /// </summary>
    public string? DuckInstanceMappingKey { get; set; }

    /// <summary>
    /// Gets or sets the canonical DuckType mapping keys used to proxy each target argument before invoking the integration.
    /// </summary>
    public List<string?> DuckParameterMappingKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the canonical DuckType mapping key used to proxy the target method return value before invoking the end callback.
    /// </summary>
    public string? DuckReturnMappingKey { get; set; }

    /// <summary>
    /// Gets or sets the canonical DuckType mapping key used to proxy the completed async result before invoking the async callback.
    /// </summary>
    public string? DuckAsyncResultMappingKey { get; set; }
}
