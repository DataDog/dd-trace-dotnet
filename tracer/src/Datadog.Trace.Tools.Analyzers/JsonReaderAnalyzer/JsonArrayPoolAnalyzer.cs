// <copyright file="JsonArrayPoolAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.Tools.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.JsonReaderAnalyzer;

/// <summary>
/// DD0016: Use JsonArrayPool.Shared with JsonTextReader/JsonTextWriter.
///
/// Every existing call site in Datadog.Trace that constructs a (vendored) Newtonsoft.Json
/// <c>JsonTextReader</c> or <c>JsonTextWriter</c> sets its <c>ArrayPool</c> property to
/// <c>Datadog.Trace.Util.Json.JsonArrayPool.Shared</c>, to avoid allocating a fresh <c>char[]</c>
/// buffer per instance on hot paths (RCM, WAF configuration loading, telemetry, feature-flag
/// parsing, etc.). This convention was flagged in code review (a "nit: use the array pool where
/// possible" suggestion) on a call site that omitted it, and the fix was accepted. This analyzer
/// makes that convention mechanically enforced, rather than relying on a reviewer to notice a
/// missing property assignment on every new usage.
///
/// The analyzer only looks at object-creation expressions for the two vendored types, and only
/// flags them when no <c>ArrayPool</c> assignment (via an object initializer or a following
/// property-assignment statement) can be found. It intentionally does not attempt any deeper
/// data-flow analysis, keeping the false-positive rate low: a reviewer who deliberately wants a
/// different pool (or no pooling) can set <c>ArrayPool</c> to something else to satisfy the rule,
/// or add a <c>#pragma warning disable DD0016</c> with a justification comment.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonArrayPoolAnalyzer : DiagnosticAnalyzer
{
    private const string JsonTextReaderTypeName = "Datadog.Trace.Vendors.Newtonsoft.Json.JsonTextReader";
    private const string JsonTextWriterTypeName = "Datadog.Trace.Vendors.Newtonsoft.Json.JsonTextWriter";
    private const string ArrayPoolPropertyName = "ArrayPool";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Diagnostics.UseJsonArrayPoolRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var readerType = compilationContext.Compilation.GetTypeByMetadataName(JsonTextReaderTypeName);
            var writerType = compilationContext.Compilation.GetTypeByMetadataName(JsonTextWriterTypeName);

            // If neither vendored type is present in this compilation, there's nothing to check
            // (and nothing to report as "missing", since these are vendored types that may not
            // be referenced by every project in the solution).
            if (readerType is null && writerType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeObjectCreation(ctx, readerType, writerType),
                SyntaxKind.ObjectCreationExpression);
        });
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? readerType, INamedTypeSymbol? writerType)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        var createdType = typeInfo.Type;
        if (createdType is null)
        {
            return;
        }

        var isReader = readerType is not null && SymbolEqualityComparer.Default.Equals(createdType, readerType);
        var isWriter = writerType is not null && SymbolEqualityComparer.Default.Equals(createdType, writerType);
        if (!isReader && !isWriter)
        {
            return;
        }

        if (HasArrayPoolAssignment(creation))
        {
            return;
        }

        var typeName = isReader ? "JsonTextReader" : "JsonTextWriter";
        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UseJsonArrayPoolRule, creation.GetLocation(), typeName));
    }

    /// <summary>
    /// Looks for an <c>ArrayPool = ...</c> assignment either in the object-initializer of the
    /// creation expression, e.g. <c>new JsonTextReader(x) { ArrayPool = JsonArrayPool.Shared }</c>,
    /// or as a subsequent simple assignment statement to a property named <c>ArrayPool</c> on the
    /// variable the creation expression is assigned to, e.g.:
    /// <code>
    /// var reader = new JsonTextReader(x);
    /// reader.ArrayPool = JsonArrayPool.Shared;
    /// </code>
    /// This deliberately doesn't validate that the assigned value is actually
    /// <c>JsonArrayPool.Shared</c> - any explicit assignment is treated as an intentional,
    /// reviewed choice, keeping the analyzer's false-positive rate low.
    /// </summary>
    private static bool HasArrayPoolAssignment(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Initializer is { } initializer
            && initializer.Expressions.Any(IsArrayPoolAssignmentExpression))
        {
            return true;
        }

        // Look for `<identifier>.ArrayPool = ...;` in the remainder of the containing block,
        // where <identifier> is the variable this creation expression was assigned to via a
        // simple `var x = new JsonTextReader(...)` or `using var x = new JsonTextReader(...)`
        // local declaration.
        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            && declarator.Parent?.Parent is LocalDeclarationStatementSyntax localDeclaration
            && localDeclaration.Parent is BlockSyntax block)
        {
            var variableName = declarator.Identifier.ValueText;
            var foundDeclaration = false;
            foreach (var statement in block.Statements)
            {
                if (!foundDeclaration)
                {
                    if (statement == localDeclaration)
                    {
                        foundDeclaration = true;
                    }

                    continue;
                }

                if (statement is ExpressionStatementSyntax
                    {
                        Expression: AssignmentExpressionSyntax
                        {
                            Left: MemberAccessExpressionSyntax
                            {
                                Expression: IdentifierNameSyntax identifier,
                                Name.Identifier.ValueText: ArrayPoolPropertyName
                            }
                        }
                    }
                    && identifier.Identifier.ValueText == variableName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsArrayPoolAssignmentExpression(ExpressionSyntax expression)
        => expression is AssignmentExpressionSyntax
        {
            Left: IdentifierNameSyntax { Identifier.ValueText: ArrayPoolPropertyName }
        };
}
