// <copyright file="ConfigurationSourceAnalyzer.cs" company="Datadog">
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
    /// Analyzer to ensure that IConfigurationSource method calls only accept string constants
    /// from PlatformKeys or ConfigurationKeys classes, not hardcoded strings or variables.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConfigurationSourceAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Diagnostic descriptor for when IConfigurationSource methods are called with a hardcoded string instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationConstantsRule = new(
            id: "DD0011",
            title: "Use configuration constants instead of hardcoded strings in IConfigurationSource method calls",
            messageFormat: "IConfigurationSource.{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of hardcoded string '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IConfigurationSource method calls should only accept string constants from PlatformKeys or ConfigurationKeys classes to ensure consistency and avoid typos.");

        /// <summary>
        /// Diagnostic descriptor for when IConfigurationSource methods are called with a variable instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationConstantsNotVariablesRule = new(
            id: "DD0012",
            title: "Use configuration constants instead of variables in IConfigurationSource method calls",
            messageFormat: "IConfigurationSource.{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of variable '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IConfigurationSource method calls should only accept string constants from PlatformKeys or ConfigurationKeys classes, not variables or computed values.");

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

            // Check if we're inside a private method or nested class of ConfigurationBuilder - if so, skip analysis
            if (IsInsideConfigurationBuilderPrivateContext(invocation, context.SemanticModel))
            {
                return;
            }

            // Check if we're inside an IConfigurationSource implementation - if so, skip analysis
            if (IsInsideConfigurationSourceImplementation(invocation, context.SemanticModel))
            {
                return;
            }

            // Check if this is an IConfigurationSource method call
            var methodName = GetConfigurationSourceMethodName(invocation, context.SemanticModel);
            if (methodName == null)
            {
                return;
            }

            // Analyze the first argument (the key parameter)
            var argumentList = invocation.ArgumentList;
            if (argumentList?.Arguments.Count > 0)
            {
                var argument = argumentList.Arguments[0]; // The key is always the first parameter
                AnalyzeConfigurationArgument(context, argument, methodName);
            }
        }

        private static bool IsInsideConfigurationBuilderPrivateContext(SyntaxNode node, SemanticModel semanticModel)
        {
            // Walk up the syntax tree to find the containing method or class
            var currentNode = node.Parent;
            while (currentNode != null)
            {
                // Check if we're inside a private nested class (like Selectors)
                if (currentNode is ClassDeclarationSyntax classDeclaration)
                {
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                    if (classSymbol is { DeclaredAccessibility: Accessibility.Private } &&
                        IsWithinConfigurationBuilder(classSymbol))
                    {
                        return true;
                    }
                }

                // Check if we're inside a private method
                if (currentNode is MethodDeclarationSyntax methodDeclaration)
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                    if (methodSymbol != null &&
                        methodSymbol.DeclaredAccessibility == Accessibility.Private &&
                        IsWithinConfigurationBuilder(methodSymbol))
                    {
                        return true;
                    }
                }

                currentNode = currentNode.Parent;
            }

            return false;
        }

        private static bool IsWithinConfigurationBuilder(ISymbol symbol)
        {
            // Check if the symbol or any of its containing types is ConfigurationBuilder
            var currentType = symbol.ContainingType;
            while (currentType != null)
            {
                if (currentType.Name == "ConfigurationBuilder" &&
                    currentType.ContainingNamespace?.ToDisplayString() == "Datadog.Trace.Configuration.Telemetry")
                {
                    return true;
                }

                currentType = currentType.ContainingType;
            }

            return false;
        }

        private static bool IsInsideConfigurationSourceImplementation(SyntaxNode node, SemanticModel semanticModel)
        {
            // Walk up the syntax tree to find the containing type
            var currentNode = node.Parent;
            while (currentNode != null)
            {
                // Check if we're inside a class or struct
                if (currentNode is ClassDeclarationSyntax classDeclaration)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                    if (typeSymbol != null && ImplementsIConfigurationSource(typeSymbol))
                    {
                        return true;
                    }
                }

                if (currentNode is StructDeclarationSyntax structDeclaration)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(structDeclaration);
                    if (typeSymbol != null && ImplementsIConfigurationSource(typeSymbol))
                    {
                        return true;
                    }
                }

                currentNode = currentNode.Parent;
            }

            return false;
        }

        private static string GetConfigurationSourceMethodName(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.ValueText;

                // Only check methods that start with "Get"
                if (!methodName.StartsWith("Get") || methodName == "IsPresent")
                {
                    return null;
                }

                // Get the symbol info for the method
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IMethodSymbol method)
                {
                    // Check if this method is from a type that implements IConfigurationSource
                    // and has at least one string parameter
                    if (ImplementsIConfigurationSource(method.ContainingType) &&
                        method.Parameters.Length > 0 &&
                        method.Parameters[0].Type?.SpecialType == SpecialType.System_String)
                    {
                        return methodName;
                    }
                }
            }

            return null;
        }

        private static bool ImplementsIConfigurationSource(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
            {
                return false;
            }

            // Check if the type itself is IConfigurationSource
            if (IsIConfigurationSource(typeSymbol))
            {
                return true;
            }

            // Check all interfaces implemented by this type
            foreach (var interfaceType in typeSymbol.AllInterfaces)
            {
                if (IsIConfigurationSource(interfaceType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIConfigurationSource(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.Name == "IConfigurationSource" &&
                   typeSymbol.ContainingNamespace?.ToDisplayString() == "Datadog.Trace.Configuration";
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
