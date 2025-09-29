// <copyright file="ConfigurationBuilderWithKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers
{
    /// <summary>
    /// Analyzer to ensure that ConfigurationBuilder.WithKeys method calls only accept string constants
    /// from PlatformKeys or ConfigurationKeys classes, not hardcoded strings or variables.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConfigurationBuilderWithKeysAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Diagnostic descriptor for when WithKeys or Or is called with a hardcoded string instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationConstantsRule = new(
            id: "DD0007",
            title: "Use configuration constants instead of hardcoded strings in WithKeys/Or calls",
            messageFormat: "{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of hardcoded string '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "ConfigurationBuilder.WithKeys and HasKeys.Or method calls should only accept string constants from PlatformKeys or ConfigurationKeys classes to ensure consistency and avoid typos.");

        /// <summary>
        /// Diagnostic descriptor for when WithKeys or Or is called with a variable instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationConstantsNotVariablesRule = new(
            id: "DD0008",
            title: "Use configuration constants instead of variables in WithKeys/Or calls",
            messageFormat: "{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of variable '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "ConfigurationBuilder.WithKeys and HasKeys.Or method calls should only accept string constants from PlatformKeys or ConfigurationKeys classes, not variables or computed values.");

        /// <summary>
        /// Gets the supported diagnostics
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UseConfigurationConstantsRule, UseConfigurationConstantsNotVariablesRule);

        /// <summary>
        /// Initialize the analyzer
        /// </summary>
        /// <param name="context">context</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check if this is a WithKeys or Or method call
            var methodName = GetConfigurationMethodName(invocation, context.SemanticModel);
            if (methodName == null)
            {
                return;
            }

            // Analyze each argument to the method
            var argumentList = invocation.ArgumentList;
            if (argumentList?.Arguments.Count > 0)
            {
                var argument = argumentList.Arguments[0]; // Both WithKeys and Or take a single string argument
                AnalyzeConfigurationArgument(context, argument, methodName);
            }
        }

        private static string GetConfigurationMethodName(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.ValueText;

                // Check if the method being called is "WithKeys" or "Or"
                const string withKeysMethodName = "WithKeys";
                const string orMethodName = "Or";
                if (methodName is withKeysMethodName or orMethodName)
                {
                    // Get the symbol info for the method
                    var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IMethodSymbol method)
                    {
                        var containingType = method.ContainingType?.Name;
                        var containingNamespace = method.ContainingNamespace?.ToDisplayString();

                        // Check if this is the ConfigurationBuilder.WithKeys method
                        if (methodName == withKeysMethodName &&
                            containingType == "ConfigurationBuilder" &&
                            containingNamespace == "Datadog.Trace.Configuration.Telemetry")
                        {
                            return withKeysMethodName;
                        }

                        // Check if this is the HasKeys.Or method
                        if (methodName == orMethodName &&
                            containingType == "HasKeys" &&
                            containingNamespace == "Datadog.Trace.Configuration.Telemetry")
                        {
                            return orMethodName;
                        }
                    }
                }
            }

            return null;
        }

        private static void AnalyzeConfigurationArgument(SyntaxNodeAnalysisContext context, ArgumentSyntax argument, string methodName)
        {
            var expression = argument.Expression;

            switch (expression)
            {
                case LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.StringLiteralToken):
                    // This is a hardcoded string literal - report diagnostic
                    var literalValue = literal.Token.ValueText;
                    var diagnostic = Diagnostic.Create(
                        UseConfigurationConstantsRule,
                        literal.GetLocation(),
                        methodName,
                        literalValue);
                    context.ReportDiagnostic(diagnostic);
                    break;

                case MemberAccessExpressionSyntax memberAccess:
                    // Check if this is accessing a constant from PlatformKeys or ConfigurationKeys
                    if (!IsValidConfigurationConstant(memberAccess, context.SemanticModel))
                    {
                        // This is accessing something else - report diagnostic
                        var memberName = memberAccess.ToString();
                        var memberDiagnostic = Diagnostic.Create(
                            UseConfigurationConstantsNotVariablesRule,
                            memberAccess.GetLocation(),
                            methodName,
                            memberName);
                        context.ReportDiagnostic(memberDiagnostic);
                    }

                    break;

                case IdentifierNameSyntax identifier:
                    // This is a variable or local constant - report diagnostic
                    var identifierName = identifier.Identifier.ValueText;
                    var variableDiagnostic = Diagnostic.Create(
                        UseConfigurationConstantsNotVariablesRule,
                        identifier.GetLocation(),
                        methodName,
                        identifierName);
                    context.ReportDiagnostic(variableDiagnostic);
                    break;

                default:
                    // Any other expression type (method calls, computed values, etc.) - report diagnostic
                    var expressionText = expression.ToString();
                    var defaultDiagnostic = Diagnostic.Create(
                        UseConfigurationConstantsNotVariablesRule,
                        expression.GetLocation(),
                        methodName,
                        expressionText);
                    context.ReportDiagnostic(defaultDiagnostic);
                    break;
            }
        }

        private static bool IsValidConfigurationConstant(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IFieldSymbol field)
            {
                // Check if this is a const string field
                if (field.IsConst && field.Type?.SpecialType == SpecialType.System_String)
                {
                    var containingType = field.ContainingType;
                    if (containingType != null)
                    {
                        // Check if the containing type is PlatformKeys or ConfigurationKeys (or their nested classes)
                        return IsValidConfigurationClass(containingType);
                    }
                }
            }

            return false;
        }

        private static bool IsValidConfigurationClass(INamedTypeSymbol typeSymbol)
        {
            // Check if this is PlatformKeys or ConfigurationKeys class or their nested classes
            var currentType = typeSymbol;
            while (currentType != null)
            {
                var typeName = currentType.Name;
                var namespaceName = currentType.ContainingNamespace?.ToDisplayString();

                // Check for PlatformKeys class
                if (typeName == "PlatformKeys" && namespaceName == "Datadog.Trace.Configuration")
                {
                    return true;
                }

                // Check for ConfigurationKeys class
                if (typeName == "ConfigurationKeys" && namespaceName == "Datadog.Trace.Configuration")
                {
                    return true;
                }

                // Check nested classes within PlatformKeys or ConfigurationKeys
                currentType = currentType.ContainingType;
            }

            return false;
        }
    }
}
