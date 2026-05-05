// <copyright file="EnumHasFlagAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer;

/// <summary>
/// Detects calls to Enum.HasFlag() which boxes both operands on .NET Framework and pre-.NET 7.
/// When a HasFlagFast() extension method is available for the enum type, a code fix can replace the call.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EnumHasFlagAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Diagnostics.EnumHasFlagBoxingRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Quick syntactic bail: must be receiver.HasFlag(arg)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (memberAccess.Name.Identifier.Text != "HasFlag")
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        // Semantic check: is it System.Enum.HasFlag?
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType.SpecialType != SpecialType.System_Enum)
        {
            return;
        }

        // Verify receiver is an enum type
        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (receiverType is null || receiverType.TypeKind != TypeKind.Enum)
        {
            return;
        }

        // Check if HasFlagFast extension method is available for this enum type
        var hasFlagFastAvailable = HasFlagFastExists(context, memberAccess.Expression, receiverType);

        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(Diagnostics.HasFlagFastAvailableKey, hasFlagFastAvailable ? "true" : "false");

        context.ReportDiagnostic(
            Diagnostic.Create(
                Diagnostics.EnumHasFlagBoxingRule,
                invocation.GetLocation(),
                properties: properties.ToImmutable()));
    }

    private static bool HasFlagFastExists(SyntaxNodeAnalysisContext context, ExpressionSyntax receiverExpression, ITypeSymbol enumType)
    {
        // Look up accessible members named "HasFlagFast" at the receiver position
        // This picks up extension methods that are in scope
        var symbols = context.SemanticModel.LookupSymbols(
            receiverExpression.SpanStart,
            container: enumType,
            name: "HasFlagFast",
            includeReducedExtensionMethods: true);

        foreach (var symbol in symbols)
        {
            if (symbol is IMethodSymbol extensionMethod
                && extensionMethod.IsExtensionMethod
                && extensionMethod.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(extensionMethod.Parameters[0].Type, enumType))
            {
                return true;
            }
        }

        return false;
    }
}
