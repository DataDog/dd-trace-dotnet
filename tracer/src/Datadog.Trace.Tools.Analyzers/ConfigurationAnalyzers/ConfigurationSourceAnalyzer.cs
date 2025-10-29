// <copyright file="ConfigurationSourceAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers
{
    /// <summary>
    /// Analyzer to ensure that specific classes' public/internal methods with string key parameters
    /// only accept string constants from PlatformKeys or ConfigurationKeys classes.
    /// Checks: IConfigurationSource implementations, ConfigurationBuilder, and other configured classes.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConfigurationSourceAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Diagnostic descriptor for when methods are called with a hardcoded string instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationConstantsRule = new(
            id: "DD0011",
            title: "Use configuration constants instead of hardcoded strings",
            messageFormat: "{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of hardcoded string '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Methods with string key parameters should only accept string constants from PlatformKeys or ConfigurationKeys classes to ensure consistency and avoid typos.");

        /// <summary>
        /// Diagnostic descriptor for when methods are called with a variable instead of a constant from PlatformKeys or ConfigurationKeys.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationConstantsNotVariablesRule = new(
            id: "DD0012",
            title: "Use configuration constants instead of variables",
            messageFormat: "{0} method should use constants from PlatformKeys or ConfigurationKeys classes instead of variable '{1}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Methods with string key parameters should only accept string constants from PlatformKeys or ConfigurationKeys classes, not variables or computed values.");

        // List of class names (with namespaces) to check for string key parameters
        private static readonly HashSet<string> ClassesToCheck =
        [
            "Datadog.Trace.Configuration.Telemetry.ConfigurationBuilder",
            "Datadog.Trace.Configuration.Telemetry.HasKeys",
            // "Datadog.Trace.Util.EnvironmentHelpers",
            // "Datadog.Trace.Util.EnvironmentHelpersNoLogging",
        ];

        private static readonly HashSet<string> InterfacesToCheck =
        [
            "Datadog.Trace.Configuration.IConfigurationSource"
        ];

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

            // Check if this is a method call we should analyze
            var (shouldAnalyze, methodName, parameterIndex) = ShouldAnalyzeMethod(invocation, context.SemanticModel);
            if (!shouldAnalyze || methodName == null)
            {
                return;
            }

            // Analyze the parameter at the specified index (the key parameter)
            var argumentList = invocation.ArgumentList;
            if (argumentList?.Arguments.Count > parameterIndex)
            {
                var argument = argumentList.Arguments[parameterIndex];
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
                    if (methodSymbol is { DeclaredAccessibility: Accessibility.Private } &&
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

        private static (bool ShouldAnalyze, string MethodName, int ParameterIndex) ShouldAnalyzeMethod(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return (false, null, 0);
            }

            var methodName = memberAccess.Name.Identifier.ValueText;

            // Get the symbol info for the method
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is not IMethodSymbol method || method.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal || method.ContainingType is null)
            {
                return (false, null, 0);
            }

            // Check if this is a method from one of the configured classes to check
            var fullTypeName = method.ContainingType.ToDisplayString();
            if (ClassesToCheck.Contains(fullTypeName) || method.ContainingType.Interfaces.Any(i => InterfacesToCheck.Contains(i.ToDisplayString())))
            {
                // Check if the method is public or internal
                // Find any string parameter named "key"–
                for (var i = 0; i < method.Parameters.Length; i++)
                {
                    var param = method.Parameters[i];
                    if (param.Type?.SpecialType == SpecialType.System_String &&
                        param.Name.Equals("key", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return (true, methodName, i);
                    }
                }
            }

            return (false, null, 0);
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
