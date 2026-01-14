// <copyright file="ConfigurationBuilderWithKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Immutable;
using Datadog.Trace.Tools.Analyzers.Helpers;
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
        /// Diagnostic descriptor for when WithKeys is called with a hardcoded string instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        private static readonly DiagnosticDescriptor UseConfigurationConstantsRule = new(
            id: "DD0007",
            title: "Use configuration constants instead of hardcoded strings in WithKeys calls",
            messageFormat: "{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of hardcoded string '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "ConfigurationBuilder.WithKeys method calls should only accept string constants from PlatformKeys or ConfigurationKeys classes to ensure consistency and avoid typos.");

        /// <summary>
        /// Diagnostic descriptor for when WithKeys is called with a variable instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        private static readonly DiagnosticDescriptor UseConfigurationConstantsNotVariablesRule = new(
            id: "DD0008",
            title: "Use configuration constants instead of variables in WithKeys calls",
            messageFormat: "{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of variable '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "ConfigurationBuilder.WithKeys method calls should only accept string constants from PlatformKeys or ConfigurationKeys classes, not variables or computed values.");

        /// <summary>
        /// Gets the supported diagnostics
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            [UseConfigurationConstantsRule, UseConfigurationConstantsNotVariablesRule, Diagnostics.MissingRequiredType];

        /// <summary>
        /// Initialize the analyzer
        /// </summary>
        /// <param name="context">context</param>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);

                var configurationBuilder = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.ConfigurationBuilder);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, configurationBuilder, nameof(ConfigurationBuilderWithKeysAnalyzer), WellKnownTypeNames.ConfigurationBuilder))
                {
                    return;
                }

                var configurationKeys = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.ConfigurationKeys);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, configurationKeys, nameof(ConfigurationBuilderWithKeysAnalyzer), WellKnownTypeNames.ConfigurationKeys))
                {
                    return;
                }

                var platformKeys = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.PlatformKeys);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, platformKeys, nameof(ConfigurationBuilderWithKeysAnalyzer), WellKnownTypeNames.PlatformKeys))
                {
                    return;
                }

                var targetTypes = new TargetTypeSymbols(configurationBuilder, configurationKeys, platformKeys);

                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeInvocationExpression(c, in targetTypes),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context, in TargetTypeSymbols targetTypes)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Bail out early: check if this is a member access with WithKeys method name or with no arguments
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
             || memberAccess.Name.Identifier.Text != WellKnownTypeNames.WithKeysMethodName
             || invocation.ArgumentList?.Arguments.Count == 0)
            {
                return;
            }

            // Check if this is a WithKeys method call
            var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is not IMethodSymbol method)
            {
                return;
            }

            // Verify it's ConfigurationBuilder.WithKeys
            if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, targetTypes.ConfigurationBuilder))
            {
                return;
            }

            // Analyze the first argument
            var argumentList = invocation.ArgumentList;
            if (argumentList?.Arguments.Count > 0)
            {
                var argument = argumentList.Arguments[0];
                AnalyzeConfigurationArgument(context, argument, WellKnownTypeNames.WithKeysMethodName, targetTypes);
            }
        }

        private static void AnalyzeConfigurationArgument(SyntaxNodeAnalysisContext context, ArgumentSyntax argument, string methodName, TargetTypeSymbols targetTypes)
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
                    if (!IsValidConfigurationConstant(memberAccess, context.SemanticModel, targetTypes))
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

        private static bool IsValidConfigurationConstant(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel, TargetTypeSymbols targetTypes)
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
                        return IsValidConfigurationClass(containingType, targetTypes);
                    }
                }
            }

            return false;
        }

        private static bool IsValidConfigurationClass(INamedTypeSymbol typeSymbol, TargetTypeSymbols targetTypes)
        {
            // Check if this is PlatformKeys or ConfigurationKeys class or their nested classes
            var currentType = typeSymbol;
            while (currentType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(currentType, targetTypes.ConfigurationKeys)
                 || SymbolEqualityComparer.Default.Equals(currentType, targetTypes.PlatformKeys))
                {
                    return true;
                }

                // Check nested classes within PlatformKeys or ConfigurationKeys
                currentType = currentType.ContainingType;
            }

            return false;
        }

        private readonly struct TargetTypeSymbols
        {
            public readonly INamedTypeSymbol ConfigurationBuilder;
            public readonly INamedTypeSymbol ConfigurationKeys;
            public readonly INamedTypeSymbol PlatformKeys;

            public TargetTypeSymbols(
                INamedTypeSymbol configurationBuilder,
                INamedTypeSymbol configurationKeys,
                INamedTypeSymbol platformKeys)
            {
                ConfigurationBuilder = configurationBuilder;
                ConfigurationKeys = configurationKeys;
                PlatformKeys = platformKeys;
            }
        }
    }
}
