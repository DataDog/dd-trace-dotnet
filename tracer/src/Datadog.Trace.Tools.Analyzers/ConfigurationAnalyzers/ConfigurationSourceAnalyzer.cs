// <copyright file="ConfigurationSourceAnalyzer.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Analyzers.ConfigurationSourceAnalyzer
{
    /// <summary>
    /// DD007: Configuration source methods should not be called outside of ConfigurationBuilder
    ///
    /// Enforces that configuration source methods (GetString, GetInt32, GetBool, GetDouble, GetAs, GetDictionary)
    /// should only be called within the ConfigurationBuilder class or its nested classes.
    /// This prevents external code from directly calling configuration source methods, ensuring proper encapsulation.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigurationSourceAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID displayed in error messages
        /// </summary>
        public const string DiagnosticId = "DD0008";

        private const string IConfigurationSourceInterface = "IConfigurationSource";
        private const string StringConfigurationSourceClass = "StringConfigurationSource";
        private const string ConfigurationBuilderClass = "ConfigurationBuilder";
        private const string GlobalConfigurationSourceClass = "GlobalConfigurationSource";

        private static readonly string[] BannedMethods =
        {
            "GetString",
            "GetInt32",
            "GetBool",
            "GetDouble",
            "GetAs",
            "GetDictionary"
        };

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Configuration source methods should not be called outside of ConfigurationBuilder",
            messageFormat: "Configuration source method '{0}' should not be called outside of ConfigurationBuilder. Use ConfigurationBuilder to access configuration values.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Configuration source methods should only be called within ConfigurationBuilder or GlobalConfigurationSource to ensure proper encapsulation and prevent misuse.");

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

            // Get the method being called
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return;
            }

            // Check if this is a banned configuration source method
            if (!IsBannedConfigurationSourceMethod(methodSymbol))
            {
                return;
            }

            // Check if we're in an allowed context (ConfigurationBuilder or GlobalConfigurationSource)
            if (IsInAllowedContext(invocation, context.SemanticModel))
            {
                return;
            }

            // Report diagnostic
            var methodName = methodSymbol.Name;
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodName);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsBannedConfigurationSourceMethod(IMethodSymbol methodSymbol)
        {
            // Check if the method name is in our banned list
            if (!BannedMethods.Contains(methodSymbol.Name))
            {
                return false;
            }

            // Check if the method is defined on a configuration source type
            var containingType = methodSymbol.ContainingType;
            if (!IsConfigurationSource(containingType))
            {
                return false;
            }

            // Only ban public interface methods that take IConfigurationTelemetry parameter
            // This excludes protected abstract methods like GetString(string key) in StringConfigurationSource
            if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            // Check if the method has IConfigurationTelemetry parameter (indicating it's a public interface method)
            return methodSymbol.Parameters.Any(p => p.Type.Name == "IConfigurationTelemetry");
        }

        private static bool IsConfigurationSource(ITypeSymbol type)
        {
            // Check if the type implements IConfigurationSource
            if (ImplementsInterface(type, IConfigurationSourceInterface))
            {
                return true;
            }

            // Check if the type inherits from StringConfigurationSource
            if (InheritsFromClass(type, StringConfigurationSourceClass))
            {
                return true;
            }

            return false;
        }

        private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        {
            return type.AllInterfaces.Any(i => i.Name == interfaceName);
        }

        private static bool InheritsFromClass(ITypeSymbol type, string className)
        {
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == className)
                {
                    return true;
                }

                baseType = baseType.BaseType;
            }

            return false;
        }

        private static bool IsInAllowedContext(SyntaxNode node, SemanticModel semanticModel)
        {
            // Walk up the syntax tree to find the containing class
            var containingClass = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingClass == null)
            {
                return false;
            }

            // Get the symbol for the containing class
            var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);
            if (classSymbol == null)
            {
                return false;
            }

            // Check if we're in ConfigurationBuilder or its nested classes
            if (IsConfigurationBuilderOrNested(classSymbol))
            {
                return true;
            }

            // Check if we're in GlobalConfigurationSource
            if (classSymbol.Name == GlobalConfigurationSourceClass)
            {
                return true;
            }

            // Check if we're in a nested class of GlobalConfigurationSource
            var containingType = classSymbol.ContainingType;
            while (containingType != null)
            {
                if (containingType.Name == GlobalConfigurationSourceClass)
                {
                    return true;
                }

                containingType = containingType.ContainingType;
            }

            // Check if we're in a ConfigurationSource class (implements IConfigurationSource or inherits from StringConfigurationSource)
            if (IsConfigurationSource(classSymbol))
            {
                return true;
            }

            // Check if we're in a nested class of a ConfigurationSource
            containingType = classSymbol.ContainingType;
            while (containingType != null)
            {
                if (IsConfigurationSource(containingType))
                {
                    return true;
                }

                containingType = containingType.ContainingType;
            }

            return false;
        }

        private static bool IsConfigurationBuilderOrNested(INamedTypeSymbol classSymbol)
        {
            // Check if this is ConfigurationBuilder itself
            if (classSymbol.Name == ConfigurationBuilderClass)
            {
                return true;
            }

            // Check if this is a nested class within ConfigurationBuilder
            var containingType = classSymbol.ContainingType;
            while (containingType != null)
            {
                if (containingType.Name == ConfigurationBuilderClass)
                {
                    return true;
                }

                containingType = containingType.ContainingType;
            }

            return false;
        }
    }
}
