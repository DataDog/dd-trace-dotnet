// <copyright file="NumericToStringInLogAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.AllocationAnalyzer;

/// <summary>
/// Detects unnecessary .ToString() calls on numeric types in IDatadogLogger log call arguments.
/// The generic log overloads handle numeric formatting without allocating a string.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NumericToStringInLogAnalyzer : DiagnosticAnalyzer
{
    private const string DatadogLoggerType = "Datadog.Trace.Logging.IDatadogLogger";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.NumericToStringInLogRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var info = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);

        if (info.Symbol is not IMethodSymbol method)
        {
            return;
        }

        // Is it an IDatadogLogger logging method?
        if (method.Name is not "Debug" and not "Information" and not "Warning" and not "Error" and not "ErrorSkipTelemetry")
        {
            return;
        }

        if (method.ContainingType.ToString() != DatadogLoggerType)
        {
            return;
        }

        // Skip object[] overloads — removing .ToString() there would just cause boxing
        if (IsObjectArrayOverload(method))
        {
            return;
        }

        // Find the messageTemplate parameter index so we know where property arguments start
        var messageTemplateIndex = -1;
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (method.Parameters[i].Name == "messageTemplate")
            {
                messageTemplateIndex = i;
                break;
            }
        }

        if (messageTemplateIndex < 0)
        {
            return;
        }

        // Scan arguments after the messageTemplate
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = messageTemplateIndex + 1; i < arguments.Count; i++)
        {
            var argExpression = arguments[i].Expression;
            CheckForNumericToString(context, argExpression);
        }
    }

    private static void CheckForNumericToString(SyntaxNodeAnalysisContext context, ExpressionSyntax argExpression)
    {
        // Check if the argument is someExpr.ToString() with no arguments
        if (argExpression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } toStringInvocation)
        {
            return;
        }

        if (memberAccess.Name.Identifier.Text != "ToString")
        {
            return;
        }

        if (toStringInvocation.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        // Check if the receiver type is a numeric type
        var receiverTypeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
        if (receiverTypeInfo.Type is null || !IsNumericType(receiverTypeInfo.Type))
        {
            return;
        }

        var receiverText = memberAccess.Expression.ToString();
        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add("ReceiverTypeName", GetCSharpKeyword(receiverTypeInfo.Type.SpecialType));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Diagnostics.NumericToStringInLogRule,
                argExpression.GetLocation(),
                properties: properties.ToImmutable(),
                receiverText));
    }

    private static bool IsObjectArrayOverload(IMethodSymbol method)
    {
        // Check if any parameter after messageTemplate is an object array
        var foundMessageTemplate = false;
        foreach (var param in method.Parameters)
        {
            if (param.Name == "messageTemplate")
            {
                foundMessageTemplate = true;
                continue;
            }

            if (foundMessageTemplate && param.Type is IArrayTypeSymbol arrayType)
            {
                var elementType = arrayType.ElementType.SpecialType;
                if (elementType is SpecialType.System_Object)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsNumericType(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_IntPtr
            or SpecialType.System_UIntPtr;

    private static string? GetCSharpKeyword(SpecialType specialType) =>
        specialType switch
        {
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_IntPtr => "nint",
            SpecialType.System_UIntPtr => "nuint",
            _ => null,
        };
}
