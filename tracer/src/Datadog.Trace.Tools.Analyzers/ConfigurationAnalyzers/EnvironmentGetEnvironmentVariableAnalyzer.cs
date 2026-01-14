// <copyright file="EnvironmentGetEnvironmentVariableAnalyzer.cs" company="Datadog">
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
    /// Analyzer to ensure that calls to specific wrapper methods only accept
    /// direct constants from the ConfigurationKeys or PlatformKeys classes.
    ///
    /// Currently checks:
    /// - EnvironmentHelpers.GetEnvironmentVariable
    /// - EnvironmentHelpersNoLogging.GetEnvironmentVariable
    /// - EnvironmentHelpers.EnvironmentVariableExists
    /// - EnvironmentHelpersNoLogging.EnvironmentVariableExists
    /// - EnvironmentVariablesProvider.GetValue
    /// - Any type implementing IValueProvider.GetValue (including through generics)
    ///
    /// Valid:   EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ApiKey)
    /// Valid:   EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.SomeKey)
    /// Valid:   valueProvider.GetValue(ConfigurationKeys.ApiKey) // where valueProvider implements IValueProvider
    /// Invalid: EnvironmentHelpers.GetEnvironmentVariable("DD_API_KEY")
    /// Invalid: EnvironmentHelpers.GetEnvironmentVariable(someVariable)
    /// Invalid: valueProvider.GetValue("DD_API_KEY")
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnvironmentGetEnvironmentVariableAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Diagnostic descriptor for when GetEnvironmentVariable is called with a hardcoded string.
        /// </summary>
        private static readonly DiagnosticDescriptor UseConfigurationKeysRule = new(
            id: "DD0011",
            title: "Use ConfigurationKeys or PlatformKeys constants in Environment.GetEnvironmentVariable calls",
            messageFormat: "Environment.GetEnvironmentVariable must use a constant from ConfigurationKeys or PlatformKeys class, not hardcoded string '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Use ConfigurationKeys.* or PlatformKeys.* constants instead of hardcoded strings to prevent typos.");

        /// <summary>
        /// Diagnostic descriptor for when GetEnvironmentVariable is called with anything other than a ConfigurationKeys or PlatformKeys constant.
        /// </summary>
        private static readonly DiagnosticDescriptor UseConfigurationKeysNotVariablesRule = new(
            id: "DD0012",
            title: "Use ConfigurationKeys or PlatformKeys constants in Environment.GetEnvironmentVariable calls",
            messageFormat: "Environment.GetEnvironmentVariable must use a constant from ConfigurationKeys or PlatformKeys class, not '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Use ConfigurationKeys.* or PlatformKeys.* constants directly. Variables, parameters, and expressions are not allowed.");

        /// <summary>
        /// Gets the supported diagnostics
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UseConfigurationKeysRule, UseConfigurationKeysNotVariablesRule, Diagnostics.MissingRequiredType);

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

                var environmentHelpers = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.EnvironmentHelpers);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, environmentHelpers, nameof(EnvironmentGetEnvironmentVariableAnalyzer), WellKnownTypeNames.EnvironmentHelpers))
                {
                    return;
                }

                var environmentHelpersNoLogging = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.EnvironmentHelpersNoLogging);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, environmentHelpersNoLogging, nameof(EnvironmentGetEnvironmentVariableAnalyzer), WellKnownTypeNames.EnvironmentHelpersNoLogging))
                {
                    return;
                }

                var iValueProvider = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.IValueProvider);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, iValueProvider, nameof(EnvironmentGetEnvironmentVariableAnalyzer), WellKnownTypeNames.IValueProvider))
                {
                    return;
                }

                var configurationKeys = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.ConfigurationKeys);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, configurationKeys, nameof(EnvironmentGetEnvironmentVariableAnalyzer), WellKnownTypeNames.ConfigurationKeys))
                {
                    return;
                }

                var platformKeys = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.PlatformKeys);
                if (Diagnostics.IsTypeNullAndReportForDatadogTrace(compilationContext, platformKeys, nameof(EnvironmentGetEnvironmentVariableAnalyzer), WellKnownTypeNames.PlatformKeys))
                {
                    return;
                }

                var targetTypes = new TargetTypeSymbols(environmentHelpers, environmentHelpersNoLogging, iValueProvider, configurationKeys, platformKeys);

                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeInvocationExpression(c, in targetTypes),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context, in TargetTypeSymbols targetTypes)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Bail out early: check if this is a member access with one of our target method names and has arguments
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
             || memberAccess.Name.Identifier.Text is not ("GetEnvironmentVariable" or "EnvironmentVariableExists" or "GetValue")
             || invocation.ArgumentList?.Arguments.Count == 0
             || invocation.ArgumentList?.Arguments.Count >= 4)
            {
                return;
            }

            var argumentList = invocation.ArgumentList;

            // Now get the semantic model (expensive operation)
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                return;
            }

            // Check if this is one of our target methods
            if (!IsTargetMethod(calledMethod, targetTypes))
            {
                return;
            }

            // Check the first argument (the environment variable name)
            if (argumentList?.Arguments.Count > 0)
            {
                var argument = argumentList.Arguments[0];
                AnalyzeEnvironmentVariableArgument(context, argument, targetTypes);
            }
        }

        /// <summary>
        /// Checks if a method is one of our target methods to analyze.
        /// </summary>
        private static bool IsTargetMethod(IMethodSymbol method, TargetTypeSymbols targetTypes)
        {
            if (method.ContainingType == null)
            {
                return false;
            }

            var containingType = method.ContainingType;

            return method.Name switch
            {
                "GetEnvironmentVariable" => SymbolEqualityComparer.Default.Equals(containingType, targetTypes.EnvironmentHelpers)
                                         || SymbolEqualityComparer.Default.Equals(containingType, targetTypes.EnvironmentHelpersNoLogging),
                "EnvironmentVariableExists" => SymbolEqualityComparer.Default.Equals(containingType, targetTypes.EnvironmentHelpers) || SymbolEqualityComparer.Default.Equals(containingType, targetTypes.EnvironmentHelpersNoLogging),
                "GetValue" => SymbolEqualityComparer.Default.Equals(containingType, targetTypes.IValueProvider)
                           || ImplementsInterface(containingType, targetTypes.IValueProvider),
                _ => false
            };
        }

        /// <summary>
        /// Checks if a type implements a specific interface.
        /// </summary>
        private static bool ImplementsInterface(ITypeSymbol type, INamedTypeSymbol targetInterface)
        {
            if (targetInterface == null)
            {
                return false;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, targetInterface))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AnalyzeEnvironmentVariableArgument(
            SyntaxNodeAnalysisContext context,
            ArgumentSyntax argument,
            TargetTypeSymbols targetTypes)
        {
            var expression = argument.Expression;

            // Only accept direct member access to ConfigurationKeys or PlatformKeys constants
            // Example: ConfigurationKeys.ApiKey, ConfigurationKeys.CIVisibility.Enabled, PlatformKeys.SomeKey
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (IsValidConfigurationConstant(memberAccess, context.SemanticModel, targetTypes))
                {
                    // Valid: This is a ConfigurationKeys or PlatformKeys constant
                    return;
                }

                // Invalid: Member access but not from ConfigurationKeys or PlatformKeys
                var diagnostic = Diagnostic.Create(
                    UseConfigurationKeysNotVariablesRule,
                    memberAccess.GetLocation(),
                    memberAccess.ToString());
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Everything else is invalid
            string errorMessage;
            DiagnosticDescriptor rule;

            if (expression is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.StringLiteralToken))
            {
                // Hardcoded string literal
                errorMessage = literal.Token.ValueText;
                rule = UseConfigurationKeysRule;
            }
            else
            {
                // Variables, parameters, method calls, expressions, etc.
                errorMessage = expression.ToString();
                rule = UseConfigurationKeysNotVariablesRule;
            }

            var invalidDiagnostic = Diagnostic.Create(
                rule,
                expression.GetLocation(),
                errorMessage);
            context.ReportDiagnostic(invalidDiagnostic);
        }

        /// <summary>
        /// Checks if a member access expression is a constant from ConfigurationKeys or PlatformKeys class.
        /// Accepts: ConfigurationKeys.ApiKey, ConfigurationKeys.CIVisibility.Enabled, PlatformKeys.*, etc.
        /// </summary>
        private static bool IsValidConfigurationConstant(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel, TargetTypeSymbols targetTypes)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IFieldSymbol field)
            {
                // Check if it's a const field
                if (!field.IsConst)
                {
                    return false;
                }

                // Check if it's from ConfigurationKeys, PlatformKeys, or any nested class within them
                var containingType = field.ContainingType;
                while (containingType != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(containingType, targetTypes.ConfigurationKeys)
                     || SymbolEqualityComparer.Default.Equals(containingType, targetTypes.PlatformKeys)
                     || IsNestedWithin(containingType, targetTypes.ConfigurationKeys)
                     || IsNestedWithin(containingType, targetTypes.PlatformKeys))
                    {
                        return true;
                    }

                    containingType = containingType.ContainingType;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a type is nested within a parent type.
        /// </summary>
        private static bool IsNestedWithin(INamedTypeSymbol type, INamedTypeSymbol parentType)
        {
            if (parentType == null)
            {
                return false;
            }

            var current = type.ContainingType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, parentType))
                {
                    return true;
                }

                current = current.ContainingType;
            }

            return false;
        }

        /// <summary>
        /// Holds references to the target type symbols to avoid repeated lookups and string allocations.
        /// </summary>
        private readonly struct TargetTypeSymbols
        {
            public readonly INamedTypeSymbol EnvironmentHelpers;
            public readonly INamedTypeSymbol EnvironmentHelpersNoLogging;
            public readonly INamedTypeSymbol IValueProvider;
            public readonly INamedTypeSymbol ConfigurationKeys;
            public readonly INamedTypeSymbol PlatformKeys;

            public TargetTypeSymbols(
                INamedTypeSymbol environmentHelpers,
                INamedTypeSymbol environmentHelpersNoLogging,
                INamedTypeSymbol iValueProvider,
                INamedTypeSymbol configurationKeys,
                INamedTypeSymbol platformKeys)
            {
                EnvironmentHelpers = environmentHelpers;
                EnvironmentHelpersNoLogging = environmentHelpersNoLogging;
                IValueProvider = iValueProvider;
                ConfigurationKeys = configurationKeys;
                PlatformKeys = platformKeys;
            }
        }
    }
}
