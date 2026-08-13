// <copyright file="ConfigurationBuilderWithKeysAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.Tools.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

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

        private static readonly DiagnosticDescriptor RedactSensitiveConfigurationRule = new(
            id: "DD0015",
            title: "Redact sensitive configuration values",
            messageFormat: "Sensitive configuration key '{0}' must be read with AsRedactedString, AsRedactedStringResult, AsRedactedDictionaryResult, or AsStringResult with compile-time recordValue: false",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Sensitive configuration values must not be recorded in configuration telemetry.");

        private static SensitiveKeysCache? _sensitiveKeysCache;

        /// <summary>
        /// Gets the supported diagnostics
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            [UseConfigurationConstantsRule, UseConfigurationConstantsNotVariablesRule, RedactSensitiveConfigurationRule, Diagnostics.MissingRequiredType];

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

                TryGetSensitiveKeys(compilationContext.Options, compilationContext.CancellationToken, out var sensitiveKeys);

                var targetTypes = new TargetTypeSymbols(configurationBuilder, configurationKeys, platformKeys);

                compilationContext.RegisterSyntaxNodeAction(
                    c => AnalyzeInvocationExpression(c, in targetTypes, sensitiveKeys),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context, in TargetTypeSymbols targetTypes, ImmutableHashSet<string> sensitiveKeys)
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
                AnalyzeConfigurationArgument(context, invocation, argument, WellKnownTypeNames.WithKeysMethodName, targetTypes, sensitiveKeys);
            }
        }

        private static void AnalyzeConfigurationArgument(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            ArgumentSyntax argument,
            string methodName,
            TargetTypeSymbols targetTypes,
            ImmutableHashSet<string> sensitiveKeys)
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
                    if (!TryGetValidConfigurationConstant(memberAccess, context.SemanticModel, targetTypes, out var field))
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
                    else if (field?.ConstantValue is string key
                          && sensitiveKeys.Contains(key)
                          && !IsRedactedRead(invocation, context.SemanticModel, context.CancellationToken, out var accessorInvocation))
                    {
                        var location = accessorInvocation is null ? invocation.GetLocation() : GetInvocationLocation(accessorInvocation);
                        context.ReportDiagnostic(Diagnostic.Create(RedactSensitiveConfigurationRule, location, key));
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

        private static bool TryGetValidConfigurationConstant(
            MemberAccessExpressionSyntax memberAccess,
            SemanticModel semanticModel,
            TargetTypeSymbols targetTypes,
            out IFieldSymbol? field)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
            {
                // Check if this is a const string field
                if (fieldSymbol.IsConst && fieldSymbol.Type?.SpecialType == SpecialType.System_String)
                {
                    var containingType = fieldSymbol.ContainingType;
                    if (containingType != null)
                    {
                        // Check if the containing type is PlatformKeys or ConfigurationKeys (or their nested classes)
                        if (IsValidConfigurationClass(containingType, targetTypes))
                        {
                            field = fieldSymbol;
                            return true;
                        }
                    }
                }
            }

            field = null;
            return false;
        }

        private static bool IsRedactedRead(
            InvocationExpressionSyntax withKeysInvocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            out InvocationExpressionSyntax? accessorInvocation)
        {
            if (semanticModel.GetOperation(withKeysInvocation, cancellationToken) is not IInvocationOperation withKeysOperation)
            {
                accessorInvocation = null;
                return false;
            }

            IOperation current = withKeysOperation;
            while (current.Parent is IParenthesizedOperation or IConversionOperation)
            {
                current = current.Parent;
            }

            IInvocationOperation? accessorOperation = current.Parent as IInvocationOperation;
            if (accessorOperation is null
             && current.Parent is IArgumentOperation argumentOperation
             && argumentOperation.Parent is IInvocationOperation extensionAccessorOperation)
            {
                accessorOperation = extensionAccessorOperation;
            }

            if (accessorOperation is null)
            {
                accessorInvocation = null;
                return false;
            }

            accessorInvocation = accessorOperation.Syntax as InvocationExpressionSyntax;
            if (accessorOperation.TargetMethod.IsStatic
             || !SymbolEqualityComparer.Default.Equals(accessorOperation.TargetMethod.ContainingType, withKeysOperation.TargetMethod.ReturnType))
            {
                return false;
            }

            if (accessorOperation.TargetMethod.Name is "AsRedactedString" or "AsRedactedStringResult" or "AsRedactedDictionaryResult")
            {
                return true;
            }

            if (accessorOperation.TargetMethod.Name != "AsStringResult")
            {
                return false;
            }

            var recordValueArgument = accessorOperation.Arguments.FirstOrDefault(x => x.Parameter?.Name == "recordValue");
            return recordValueArgument?.Value.ConstantValue is { HasValue: true, Value: false };
        }

        private static Location GetInvocationLocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return Location.Create(invocation.SyntaxTree, TextSpan.FromBounds(memberAccess.Name.SpanStart, invocation.Span.End));
            }

            return invocation.GetLocation();
        }

        private static bool TryGetSensitiveKeys(AnalyzerOptions options, CancellationToken cancellationToken, out ImmutableHashSet<string> sensitiveKeys)
        {
            var file = options.AdditionalFiles.FirstOrDefault(
                x => Path.GetFileName(x.Path).Equals(Constants.SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase));
            if (file is null)
            {
                sensitiveKeys = ImmutableHashSet<string>.Empty;
                return false;
            }

            try
            {
                var content = file.GetText(cancellationToken)?.ToString();
                if (string.IsNullOrEmpty(content))
                {
                    sensitiveKeys = ImmutableHashSet<string>.Empty;
                    return false;
                }

                var cached = Volatile.Read(ref _sensitiveKeysCache);
                if (cached is not null && cached.Content == content)
                {
                    sensitiveKeys = cached.Keys;
                    return true;
                }

                var configurations = YamlReader.ParseSupportedConfigurations(content!).Configurations;
                if (configurations.Count == 0)
                {
                    sensitiveKeys = ImmutableHashSet<string>.Empty;
                    return false;
                }

                var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
                foreach (var configuration in configurations)
                {
                    if (configuration.Value.Sensitive)
                    {
                        builder.Add(configuration.Key);
                    }
                }

                sensitiveKeys = builder.ToImmutable();
                Interlocked.Exchange(ref _sensitiveKeysCache, new SensitiveKeysCache(content!, sensitiveKeys));
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                sensitiveKeys = ImmutableHashSet<string>.Empty;
                return false;
            }
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

        private sealed class SensitiveKeysCache
        {
            public SensitiveKeysCache(string content, ImmutableHashSet<string> keys)
            {
                Content = content;
                Keys = keys;
            }

            public string Content { get; }

            public ImmutableHashSet<string> Keys { get; }
        }
    }
}
