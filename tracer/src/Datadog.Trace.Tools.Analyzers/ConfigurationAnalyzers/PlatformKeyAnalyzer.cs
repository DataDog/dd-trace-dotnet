// <copyright file="PlatformKeyAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers
{
    /// <summary>
    /// Analyzer that validates IConfigKey implementations.
    /// Ensures that GetKey() methods return constant strings for configuration keys (DD_*, _DD_*, DATADOG_*, OTEL_*).
    /// Only generated ConfigKey structs in the Generated namespace are allowed to return these prefixes.
    /// Manual implementations must return constants and cannot use these reserved prefixes.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PlatformKeyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DD0009";
        public const string NonConstantDiagnosticId = "DD0010";
        private const string Category = "Configuration";

        private static readonly LocalizableString Title = "IConfigKey GetKey() returns a configuration key reserved for generated types";
        private static readonly LocalizableString MessageFormat = "IConfigKey implementation '{0}' returns '{1}' which starts with '{2}'. Configuration keys (DD_*, _DD_*, DATADOG_*, OTEL_*) are reserved for auto-generated ConfigKey structs from supported-configurations.json. If you wanna add a new configuration key, add it to supported-configurations.json and let the source generator create the struct.";
        private static readonly LocalizableString Description = "IConfigKey implementations should only return platform environment variable names (e.g., WEBSITE_SITE_NAME, AWS_LAMBDA_FUNCTION_NAME) or use the Generated namespace. Configuration keys starting with DD_, _DD_, DATADOG_, or OTEL are reserved for auto-generated types.";

        private static readonly LocalizableString NonConstantTitle = "IConfigKey GetKey() must return a constant string";
        private static readonly LocalizableString NonConstantMessageFormat = "IConfigKey implementation '{0}' GetKey() method must return a constant string value, not a variable or expression";
        private static readonly LocalizableString NonConstantDescription = "All IConfigKey implementations must return a compile-time constant string from GetKey() to ensure predictable behavior and enable compile-time validation.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        private static readonly DiagnosticDescriptor NonConstantRule = new(
            NonConstantDiagnosticId,
            NonConstantTitle,
            NonConstantMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: NonConstantDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, NonConstantRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeStructDeclaration, SyntaxKind.StructDeclaration);
        }

        private static void AnalyzeStructDeclaration(SyntaxNodeAnalysisContext context)
        {
            var structDeclaration = (StructDeclarationSyntax)context.Node;
            var structSymbol = context.SemanticModel.GetDeclaredSymbol(structDeclaration);

            if (structSymbol == null)
            {
                return;
            }

            // Check if the struct implements IConfigKey
            var implementIsConfigKey = structSymbol.Interfaces.Any(i => i.Name == "IConfigKey");
            if (!implementIsConfigKey)
            {
                return;
            }

            // Skip if in the Generated namespace (auto-generated types are allowed to use DD_* prefixes)
            if (IsInGeneratedNamespace(structSymbol))
            {
                return;
            }

            // Skip integration key types that dynamically construct keys using string.Format
            if (IsExcludedType(structSymbol))
            {
                return;
            }

            // Find the GetKey() method
            var getKeyMethod = structSymbol.GetMembers("GetKey")
                                           .OfType<IMethodSymbol>()
                                           .FirstOrDefault();

            if (getKeyMethod == null)
            {
                return;
            }

            // Analyze the GetKey() method implementation
            var methodSyntax = getKeyMethod.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            if (methodSyntax == null)
            {
                return;
            }

            // Get the expression being returned (handles both arrow expressions and return statements)
            ExpressionSyntax returnExpression = null;
            Location diagnosticLocation = null;

            // Check for arrow expression body (e.g., public string GetKey() => "value";)
            if (methodSyntax.ExpressionBody != null)
            {
                returnExpression = methodSyntax.ExpressionBody.Expression;
                diagnosticLocation = methodSyntax.ExpressionBody.Expression.GetLocation();
            }
            else
            {
                // Look for return statement in method body
                var returnStatement = methodSyntax.DescendantNodes()
                                                  .OfType<ReturnStatementSyntax>()
                                                  .FirstOrDefault();

                if (returnStatement?.Expression != null)
                {
                    returnExpression = returnStatement.Expression;
                    diagnosticLocation = returnStatement.GetLocation();
                }
            }

            if (returnExpression == null || diagnosticLocation == null)
            {
                return;
            }

            // Try to get the constant value being returned
            var constantValue = context.SemanticModel.GetConstantValue(returnExpression);

            // Check if it's NOT a constant - report DD0010
            if (!constantValue.HasValue)
            {
                var diagnostic = Diagnostic.Create(
                    NonConstantRule,
                    diagnosticLocation,
                    structSymbol.Name);

                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Check if it's a string constant
            if (!(constantValue.Value is string keyValue))
            {
                return;
            }

            // Check if the key starts with reserved prefixes - report DD0009
            var reservedPrefixes = new[] { "DD_", "_DD_", "DATADOG_", "OTEL_" };
            foreach (var prefix in reservedPrefixes)
            {
                if (keyValue.StartsWith(prefix))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        diagnosticLocation,
                        structSymbol.Name,
                        keyValue,
                        prefix);

                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        private static bool IsInGeneratedNamespace(INamedTypeSymbol symbol)
        {
            var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
            return namespaceName != null && namespaceName.EndsWith(".Generated");
        }

        private static bool IsExcludedType(INamedTypeSymbol symbol)
        {
            // ConfigKeyAlias is a nested type in ConfigurationBuilder that wraps alias keys
            if (symbol.Name == "ConfigKeyAlias" &&
                symbol.ContainingType?.Name == "ConfigurationBuilder" &&
                symbol.ContainingNamespace.ToDisplayString() == "Datadog.Trace.Configuration.Telemetry")
            {
                return true;
            }

            // These types dynamically construct configuration keys using string.Format
            var integrationKeyTypes = new[]
            {
                "IntegrationNameConfigKey",
                "IntegrationAnalyticsEnabledConfigKey",
                "IntegrationAnalyticsSampleRateConfigKey",
                "ConfigKeyProfilerLogPath",
            };

            return integrationKeyTypes.Contains(symbol.Name);
        }

        private static bool ImplementsIConfigKey(INamedTypeSymbol symbol)
        {
            return symbol.Interfaces.Any(i => i.Name == "IConfigKey");
        }
    }
}
