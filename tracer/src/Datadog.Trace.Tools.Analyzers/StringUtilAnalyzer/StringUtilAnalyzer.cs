// <copyright file="StringUtilAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.StringUtilAnalyzer;

/// <summary>
/// An analyzer that flags calls to <see cref="string.IsNullOrEmpty(string)"/> and
/// <see cref="string.IsNullOrWhiteSpace(string)"/>, recommending the repository's
/// <c>StringUtil</c> wrapper instead, which adds the <c>[NotNullWhen(false)]</c> nullable
/// annotation that the BCL methods lack on .NET Framework and .NET Standard 2.0.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StringUtilAnalyzer : DiagnosticAnalyzer
{
    private const string IsNullOrEmpty = "IsNullOrEmpty";
    private const string IsNullOrWhiteSpace = "IsNullOrWhiteSpace";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Diagnostics.UseStringUtilRule);

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

        // Only interested in simple `string.IsNullOrEmpty(...)` / `string.IsNullOrWhiteSpace(...)`
        // member-access calls, not e.g. calls already made through StringUtil, or via an alias.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not (IsNullOrEmpty or IsNullOrWhiteSpace))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol { IsStatic: true } method)
        {
            return;
        }

        // Confirm this resolves to System.String's own static method, not some other type's
        // same-named method (including StringUtil itself, which would otherwise self-flag).
        if (method.ContainingType?.SpecialType != SpecialType.System_String)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UseStringUtilRule, memberAccess.Name.GetLocation(), methodName));
    }
}
