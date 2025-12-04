// <copyright file="EnvironmentGetEnvironmentVariableAnalyzer.cs" company="Datadog">
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
    /// Analyzer to ensure that calls to specific wrapper methods only accept
    /// direct constants from the ConfigurationKeys or PlatformKeys classes.
    ///
    /// Currently checks:
    /// - EnvironmentHelpers.GetEnvironmentVariable
    ///
    /// To add more methods, add them to the TargetMethods list.
    ///
    /// Valid:   EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ApiKey)
    /// Valid:   EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.SomeKey)
    /// Invalid: EnvironmentHelpers.GetEnvironmentVariable("DD_API_KEY")
    /// Invalid: EnvironmentHelpers.GetEnvironmentVariable(someVariable)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnvironmentGetEnvironmentVariableAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Diagnostic descriptor for when GetEnvironmentVariable is called with a hardcoded string.
        /// </summary>
        public static readonly DiagnosticDescriptor UseConfigurationKeysRule = new(
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
        public static readonly DiagnosticDescriptor UseConfigurationKeysNotVariablesRule = new(
            id: "DD0012",
            title: "Use ConfigurationKeys or PlatformKeys constants in Environment.GetEnvironmentVariable calls",
            messageFormat: "Environment.GetEnvironmentVariable must use a constant from ConfigurationKeys or PlatformKeys class, not '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Use ConfigurationKeys.* or PlatformKeys.* constants directly. Variables, parameters, and expressions are not allowed.");

        /// <summary>
        /// List of methods to check. Add new entries here to extend the analyzer.
        /// Format: ("Fully.Qualified.TypeName", "MethodName")
        /// </summary>
        private static readonly (string TypeName, string MethodName)[] TargetMethods =
        {
            ("Datadog.Trace.Util.EnvironmentHelpers", "GetEnvironmentVariable"),
            ("Datadog.Trace.Util.EnvironmentHelpers", "EnvironmentVariableExists"),
            ("Datadog.Trace.Util.EnvironmentHelpersNoLogging", "GetEnvironmentVariable"),
            ("Datadog.Trace.Util.EnvironmentHelpersNoLogging", "TryCheckEnvVar"),
        };

        /// <summary>
        /// Gets the supported diagnostics
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(UseConfigurationKeysRule, UseConfigurationKeysNotVariablesRule);

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

            // Get the method being called
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                return;
            }

            // Check if this is one of our target methods
            if (!IsTargetMethod(calledMethod))
            {
                return;
            }

            // Check the first argument (the environment variable name)
            var argumentList = invocation.ArgumentList;
            if (argumentList?.Arguments.Count > 0)
            {
                var argument = argumentList.Arguments[0];
                AnalyzeEnvironmentVariableArgument(context, argument);
            }
        }

        /// <summary>
        /// Checks if a method is one of our target methods to analyze.
        /// </summary>
        private static bool IsTargetMethod(IMethodSymbol method)
        {
            var containingType = method.ContainingType?.ToDisplayString();
            var methodName = method.Name;

            foreach (var (targetType, targetMethod) in TargetMethods)
            {
                if (containingType == targetType && methodName == targetMethod)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AnalyzeEnvironmentVariableArgument(
            SyntaxNodeAnalysisContext context,
            ArgumentSyntax argument)
        {
            var expression = argument.Expression;

            // Only accept direct member access to ConfigurationKeys or PlatformKeys constants
            // Example: ConfigurationKeys.ApiKey, ConfigurationKeys.CIVisibility.Enabled, PlatformKeys.SomeKey
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (IsValidConfigurationConstant(memberAccess, context.SemanticModel))
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
        private static bool IsValidConfigurationConstant(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
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
                    var fullTypeName = containingType.ToDisplayString();
                    const string configKeys = "Datadog.Trace.Configuration.ConfigurationKeys";
                    // Check if this is ConfigurationKeys or a nested class within ConfigurationKeys
                    if (fullTypeName == configKeys || fullTypeName.StartsWith(configKeys))
                    {
                        return true;
                    }

                    const string platformKeys = "Datadog.Trace.Configuration.PlatformKeys";
                    // Check if this is PlatformKeys or a nested class within PlatformKeys
                    if (fullTypeName == platformKeys || fullTypeName.StartsWith(platformKeys))
                    {
                        return true;
                    }

                    containingType = containingType.ContainingType;
                }
            }

            return false;
        }
    }
}
