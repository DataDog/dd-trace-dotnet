// <copyright file="Diagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// Helper class for holding all the diagnostic definitions
/// </summary>
public class Diagnostics
{
    /// <summary>
    /// The DiagnosticID for <see cref="ExceptionRule"/>
    /// </summary>
    public const string ExceptionDiagnosticId = "DDLOG001";

    /// <summary>
    /// The DiagnosticID for <see cref="TemplateRule"/>
    /// </summary>
    public const string TemplateDiagnosticId = "DDLOG002";

    /// <summary>
    /// The DiagnosticID for <see cref="PropertyBindingRule"/>
    /// </summary>
    public const string PropertyBindingDiagnosticId = "DDLOG003";

    /// <summary>
    /// The DiagnosticID for <see cref="ConstantMessageTemplateRule"/>
    /// </summary>
    public const string ConstantMessageTemplateDiagnosticId = "DDLOG004";

    /// <summary>
    /// The DiagnosticID for <see cref="UniquePropertyNameRule"/>
    /// </summary>
    public const string UniquePropertyNameDiagnosticId = "DDLOG005";

    /// <summary>
    /// The DiagnosticID for <see cref="PascalPropertyNameRule"/>
    /// </summary>
    public const string PascalPropertyNameDiagnosticId = "DDLOG006";

    /// <summary>
    /// The DiagnosticID for <see cref="DestructureAnonymousObjectsRule"/>
    /// </summary>
    public const string DestructureAnonymousObjectsDiagnosticId = "DDLOG007";

    /// <summary>
    /// The DiagnosticID for <see cref="UseCorrectContextualLoggerRule"/>
    /// </summary>
    public const string UseCorrectContextualLoggerDiagnosticId = "DDLOG008";

    /// <summary>
    /// The DiagnosticID for <see cref="UseDatadogLoggerRule"/>
    /// </summary>
    public const string UseDatadogLoggerDiagnosticId = "DDLOG009";

    internal static readonly DiagnosticDescriptor ExceptionRule = new(
        ExceptionDiagnosticId,
        title: "Exception not passed as first argument",
        messageFormat: "The exception '{0}' should be passed as first argument",
        "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Exceptions should be passed in the Exception Parameter.");

    internal static readonly DiagnosticDescriptor TemplateRule = new(
        TemplateDiagnosticId,
        title: "Invalid template",
        messageFormat: "{0}",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Checks for errors in the MessageTemplate.");

    internal static readonly DiagnosticDescriptor PropertyBindingRule = new(
        PropertyBindingDiagnosticId,
        title: "Invalid properties",
        messageFormat: "{0}",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Checks whether properties and arguments match up.");

    internal static readonly DiagnosticDescriptor ConstantMessageTemplateRule = new(
        ConstantMessageTemplateDiagnosticId,
        title: "Message templates should be constant",
        messageFormat: "MessageTemplate argument {0} is not constant",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "MessageTemplate must be a constant value to ensure caching and avoid interpolation issues.");

    internal static readonly DiagnosticDescriptor UniquePropertyNameRule = new(
        UniquePropertyNameDiagnosticId,
        title: "Duplicate property name",
        messageFormat: "Property name '{0}' is not unique in this MessageTemplate",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All property names in a MessageTemplate must be unique.");

    internal static readonly DiagnosticDescriptor PascalPropertyNameRule = new(
        PascalPropertyNameDiagnosticId,
        title: "Incorrect property name format",
        messageFormat: "Property name '{0}' should be pascal case",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Property names in a MessageTemplates should be Pascal Case for consistency.");

    internal static readonly DiagnosticDescriptor DestructureAnonymousObjectsRule = new(
        DestructureAnonymousObjectsDiagnosticId,
        title: "Incorrect anonymous object usage",
        messageFormat: "Property '{0}' should use destructuring because the argument is an anonymous object",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Anonymous objects should use the '@' hint to ensure they are destructed.");

    internal static readonly DiagnosticDescriptor UseCorrectContextualLoggerRule = new(
        UseCorrectContextualLoggerDiagnosticId,
        title: "Incorrect type argument",
        messageFormat: "Logger '{0}' should use {1} instead of {2}",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Logger instances should use the current class for context.");

    internal static readonly DiagnosticDescriptor UseDatadogLoggerRule = new(
        UseDatadogLoggerDiagnosticId,
        title: "Incorrect logger type",
        messageFormat: "Incorrect use of Serilog ILogger. Use IDatadogLogger instead.",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "You should use the IDatadogLogger wrapper for logging.");
}
