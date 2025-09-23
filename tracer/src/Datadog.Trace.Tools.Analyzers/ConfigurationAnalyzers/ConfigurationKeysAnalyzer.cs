// <copyright file="ConfigurationKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// DD006: WithKeys method must use constants from ConfigurationKeys or PlatformKeys
///
/// Enforces that calls to WithKeys method only accept string constants from the
/// ConfigurationKeys or PlatformKeys classes, not hardcoded string literals.
/// This ensures consistency and prevents typos in configuration key usage.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigurationKeysAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The diagnostic ID displayed in error messages for WithKeys validation
    /// </summary>
    public const string DiagnosticId = "DD0006";

    /// <summary>
    /// The diagnostic ID for PlatformKeys prefix validation
    /// </summary>
    public const string PlatformKeysPrefixDiagnosticId = "DD0007";

    private const string WithKeysMethodName = "WithKeys";
    private const string ConfigurationKeysClassName = "ConfigurationKeys";
    private const string PlatformKeysClassName = "PlatformKeys";

    private static readonly DiagnosticDescriptor WithKeysRule = new(
        DiagnosticId,
        title: "WithKeys method must use appropriate constants",
        messageFormat: "WithKeys method calls should use constants from ConfigurationKeys or PlatformKeys classes instead of hardcoded strings. Use '{0}' instead of the string literal.",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "WithKeys method calls should use constants from ConfigurationKeys or PlatformKeys classes instead of hardcoded strings to ensure consistency and prevent typos.");

    private static readonly DiagnosticDescriptor PlatformKeysPrefixRule = new(
        PlatformKeysPrefixDiagnosticId,
        title: "PlatformKeys constants must not start with DD_ or OTEL_",
        messageFormat: "PlatformKeys constant '{0}' with value '{1}' should not start with 'DD_' or 'OTEL_'. Platform keys should contain platform-specific environment variables, not Datadog or OpenTelemetry configuration keys.",
        category: "CodeQuality",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "PlatformKeys constants should contain platform-specific environment variables and must not start with 'DD_' or 'OTEL_' prefixes, the latter should be in ConfigurationKeys and present in the supported-configurations.json file.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(WithKeysRule, PlatformKeysPrefixRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzePlatformKeysConstants, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a WithKeys method call
        if (!IsWithKeysMethodCall(invocation, context.SemanticModel))
        {
            return;
        }

        // Analyze each argument to the WithKeys method
        var argumentList = invocation.ArgumentList;
        if (argumentList.Arguments.Count > 0)
        {
            foreach (var argument in argumentList.Arguments)
            {
                AnalyzeArgument(context, argument);
            }
        }
    }

    private static bool IsWithKeysMethodCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Check if the method name is WithKeys
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess?.Name.Identifier.ValueText != WithKeysMethodName)
        {
            return false;
        }

        // Get the symbol information for the method
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        return symbolInfo.Symbol is IMethodSymbol;
    }

    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context, ArgumentSyntax argument)
    {
        var expression = argument.Expression;

        // Skip if it's already using ConfigurationKeys or PlatformKeys
        if (IsValidKeysReference(expression))
        {
            return;
        }

        // Report diagnostic for any non-constant expression
        var suggestedConstant = "appropriate constant";
        // Try to find a matching key if it's a string literal
        if (expression is LiteralExpressionSyntax literal &&
            literal.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            var stringValue = literal.Token.ValueText;
            var matchingKey = FindMatchingKey(context, stringValue);
            if (matchingKey != null)
            {
                suggestedConstant = matchingKey;
            }
        }

        var diagnostic = Diagnostic.Create(
            WithKeysRule,
            expression.GetLocation(),
            suggestedConstant);

        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsValidKeysReference(ExpressionSyntax expression)
    {
        return expression switch
        {
            // Direct reference: ConfigurationKeys.SomeKey or PlatformKeys.SomeKey
            MemberAccessExpressionSyntax memberAccess =>
                IsKeysType(memberAccess.Expression, ConfigurationKeysClassName) ||
                IsKeysType(memberAccess.Expression, PlatformKeysClassName),

            // Qualified reference: Datadog.Trace.Configuration.ConfigurationKeys.SomeKey or PlatformKeys.SomeKey
            _ when expression.ToString().Contains($".{ConfigurationKeysClassName}.") ||
                   expression.ToString().Contains($".{PlatformKeysClassName}.") => true,

            _ => false
        };
    }

    private static bool IsKeysType(ExpressionSyntax expression, string keysClassName)
    {
        return expression switch
        {
            // Simple reference: ConfigurationKeys or PlatformKeys
            IdentifierNameSyntax identifier =>
                identifier.Identifier.ValueText == keysClassName,

            // Nested reference: ConfigurationKeys.Something or PlatformKeys.AzureAppService
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText == keysClassName ||
                IsKeysType(memberAccess.Expression, keysClassName),

            _ => false
        };
    }

    private static string? FindMatchingKey(SyntaxNodeAnalysisContext context, string stringValue)
    {
        var compilation = context.SemanticModel.Compilation;
        // Check both ConfigurationKeys and PlatformKeys
        var typeNames = new[]
        {
            "Datadog.Trace.Configuration.ConfigurationKeys",
            "Datadog.Trace.Configuration.PlatformKeys"
        };

        foreach (var typeName in typeNames)
        {
            var keysType = compilation.GetTypesByMetadataName(typeName).FirstOrDefault();
            if (keysType == null)
            {
                continue;
            }

            // Look for a field with the same constant value in the main type and all nested types
            var allTypes = new[] { keysType }.Concat(keysType.GetTypeMembers()).ToArray();

            foreach (var type in allTypes)
            {
                var matchingField = type
                                   .GetMembers()
                                   .OfType<IFieldSymbol>()
                                   .Where(f => f.IsConst && f.Type.SpecialType == SpecialType.System_String)
                                   .FirstOrDefault(f => f.ConstantValue?.ToString() == stringValue);

                if (matchingField != null)
                {
                    var className = typeName.EndsWith("ConfigurationKeys") ? "ConfigurationKeys" : "PlatformKeys";
                    return $"{className}.{matchingField.Name}";
                }
            }
        }

        return null;
    }

    private static void AnalyzePlatformKeysConstants(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

        // Check if this field is in a PlatformKeys class or nested class
        if (!IsInPlatformKeysClass(fieldDeclaration))
        {
            return;
        }

        // Check if this is a const string field
        if (!IsConstStringField(fieldDeclaration))
        {
            return;
        }

        // Analyze each variable declarator in the field declaration
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (variable.Initializer?.Value is LiteralExpressionSyntax literal &&
                literal.Token.IsKind(SyntaxKind.StringLiteralToken))
            {
                var constantValue = literal.Token.ValueText;
                var fieldName = variable.Identifier.ValueText;

                // Check if the constant value starts with DD_ or OTEL_
                if (constantValue.StartsWith("DD_") || constantValue.StartsWith("OTEL_"))
                {
                    var diagnostic = Diagnostic.Create(
                        PlatformKeysPrefixRule,
                        variable.GetLocation(),
                        fieldName,
                        constantValue);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool IsInPlatformKeysClass(FieldDeclarationSyntax fieldDeclaration)
    {
        var parent = fieldDeclaration.Parent;
        while (parent != null)
        {
            if (parent is ClassDeclarationSyntax classDecl)
            {
                // Check if this class or any parent class is named PlatformKeys
                if (classDecl.Identifier.ValueText == PlatformKeysClassName)
                {
                    return true;
                }

                // Check if this is a nested class within PlatformKeys
                var parentClass = classDecl.Parent as ClassDeclarationSyntax;
                if (parentClass?.Identifier.ValueText == PlatformKeysClassName)
                {
                    return true;
                }
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static bool IsConstStringField(FieldDeclarationSyntax fieldDeclaration)
    {
        // Check if the field has const modifier
        if (!fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
        {
            return false;
        }

        // Check if the field type is string
        var variableDeclaration = fieldDeclaration.Declaration;
        if (variableDeclaration.Type is PredefinedTypeSyntax predefinedType)
        {
            return predefinedType.Keyword.IsKind(SyntaxKind.StringKeyword);
        }

        // Handle cases where string is referenced as System.String or fully qualified
        return variableDeclaration.Type?.ToString().Contains("string") == true;
    }
}
