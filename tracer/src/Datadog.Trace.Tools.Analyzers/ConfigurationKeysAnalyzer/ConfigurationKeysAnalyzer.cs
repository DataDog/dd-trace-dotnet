// <copyright file="ConfigurationKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationKeysAnalyzer
{
    /// <summary>
    /// DD006: WithKeys method must use appropriate constants
    ///
    /// Enforces that calls to ConfigurationBuilder.WithKeys method only accept
    /// string constants from the ConfigurationKeys class, and PlatformConfigurationBuilder.WithKeys
    /// only accepts constants from the PlatformKeys class, not hardcoded string literals.
    /// This ensures consistency and prevents typos in configuration key usage.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigurationKeysAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID displayed in error messages
        /// </summary>
        public const string DiagnosticId = "DD0006";

        private const string WithKeysMethodName = "WithKeys";
        private const string ConfigurationKeysClassName = "ConfigurationKeys";
        private const string PlatformKeysClassName = "PlatformKeys";
        private const string ConfigurationBuilderTypeName = "ConfigurationBuilder";
        private const string PlatformConfigurationBuilderTypeName = "PlatformConfigurationBuilder";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "WithKeys method must use appropriate constants",
            messageFormat: "WithKeys method calls should use constants from {0} class instead of hardcoded strings. Use '{0}.{1}' instead of the string literal.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "WithKeys method calls should use constants from the appropriate keys class instead of hardcoded strings to ensure consistency and prevent typos.");

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check if this is a WithKeys method call and get the expected keys class
            var expectedKeysClass = GetExpectedKeysClass(invocation, context.SemanticModel);
            if (expectedKeysClass == null)
            {
                return;
            }

            // Analyze each argument to the WithKeys method
            var argumentList = invocation.ArgumentList;
            if (argumentList?.Arguments.Count > 0)
            {
                foreach (var argument in argumentList.Arguments)
                {
                    AnalyzeArgument(context, argument, expectedKeysClass);
                }
            }
        }

        private static string? GetExpectedKeysClass(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            // Check if the method name is WithKeys
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess?.Name.Identifier.ValueText != WithKeysMethodName)
            {
                return null;
            }

            // Get the symbol information for the method
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return null;
            }

            // Check the containing type and return the appropriate keys class
            var containingType = methodSymbol.ContainingType;
            return containingType?.Name switch
            {
                ConfigurationBuilderTypeName => ConfigurationKeysClassName,
                PlatformConfigurationBuilderTypeName => PlatformKeysClassName,
                _ => null
            };
        }

        private static void AnalyzeArgument(SyntaxNodeAnalysisContext context, ArgumentSyntax argument, string expectedKeysClass)
        {
            var expression = argument.Expression;

            // Skip if it's already using the expected keys class
            if (IsExpectedKeysReference(expression, expectedKeysClass))
            {
                return;
            }

            // Check if it's using the wrong keys class (e.g., PlatformKeys in ConfigurationBuilder)
            if (IsWrongKeysClassReference(expression, expectedKeysClass))
            {
                var wrongKeysClass = GetWrongKeysClass(expectedKeysClass);
                var diagnostic = Diagnostic.Create(
                    Rule,
                    expression.GetLocation(),
                    expectedKeysClass,
                    $"Move this key to {expectedKeysClass} or use {wrongKeysClass}ConfigurationBuilder");

                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Check if it's a string literal
            if (expression is LiteralExpressionSyntax literal &&
                literal.Token.IsKind(SyntaxKind.StringLiteralToken))
            {
                var stringValue = literal.Token.ValueText;
                var suggestedConstant = FindMatchingKey(context, stringValue, expectedKeysClass);

                var diagnostic = Diagnostic.Create(
                    Rule,
                    literal.GetLocation(),
                    expectedKeysClass,
                    suggestedConstant ?? "appropriate constant");

                context.ReportDiagnostic(diagnostic);
            }

            // Check if it's a string interpolation or concatenation
            else if (expression is InterpolatedStringExpressionSyntax ||
                     (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)))
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    expression.GetLocation(),
                    expectedKeysClass,
                    "appropriate constant");

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsExpectedKeysReference(ExpressionSyntax expression, string expectedKeysClass)
        {
            return expression switch
            {
                // Direct reference: ConfigurationKeys.SomeKey or PlatformKeys.SomeKey
                MemberAccessExpressionSyntax memberAccess =>
                    IsExpectedKeysType(memberAccess.Expression, expectedKeysClass),

                // Qualified reference: Datadog.Trace.Configuration.ConfigurationKeys.SomeKey
                _ when expression.ToString().Contains($".{expectedKeysClass}.") => true,

                _ => false
            };
        }

        private static bool IsExpectedKeysType(ExpressionSyntax expression, string expectedKeysClass)
        {
            return expression switch
            {
                // Simple reference: ConfigurationKeys or PlatformKeys
                IdentifierNameSyntax identifier =>
                    identifier.Identifier.ValueText == expectedKeysClass,

                // Nested reference: ConfigurationKeys.Something or PlatformKeys.AzureAppService
                MemberAccessExpressionSyntax memberAccess =>
                    memberAccess.Name.Identifier.ValueText == expectedKeysClass ||
                    IsExpectedKeysType(memberAccess.Expression, expectedKeysClass),

                _ => false
            };
        }

        private static bool IsWrongKeysClassReference(ExpressionSyntax expression, string expectedKeysClass)
        {
            var wrongKeysClass = GetWrongKeysClass(expectedKeysClass);
            if (wrongKeysClass == null)
            {
                return false;
            }

            return expression switch
            {
                // Direct reference: PlatformKeys.SomeKey when expecting ConfigurationKeys
                MemberAccessExpressionSyntax memberAccess =>
                    IsExpectedKeysType(memberAccess.Expression, wrongKeysClass),

                // Qualified reference: Datadog.Trace.Configuration.PlatformKeys.SomeKey
                _ when expression.ToString().Contains($".{wrongKeysClass}.") => true,

                _ => false
            };
        }

        private static string? GetWrongKeysClass(string expectedKeysClass)
        {
            return expectedKeysClass switch
            {
                ConfigurationKeysClassName => PlatformKeysClassName,
                PlatformKeysClassName => ConfigurationKeysClassName,
                _ => null
            };
        }

        private static string? FindMatchingKey(SyntaxNodeAnalysisContext context, string stringValue, string expectedKeysClass)
        {
            // Try to find a matching constant in the expected keys class
            var compilation = context.SemanticModel.Compilation;
            var fullTypeName = expectedKeysClass switch
            {
                ConfigurationKeysClassName => "Datadog.Trace.Configuration.ConfigurationKeys",
                PlatformKeysClassName => "Datadog.Trace.Configuration.PlatformKeys",
                _ => null
            };

            if (fullTypeName == null)
            {
                return null;
            }

            var keysType = compilation.GetTypesByMetadataName(fullTypeName).FirstOrDefault();
            if (keysType == null)
            {
                return null;
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
                    return matchingField.Name;
                }
            }

            return null;
        }
    }
}
