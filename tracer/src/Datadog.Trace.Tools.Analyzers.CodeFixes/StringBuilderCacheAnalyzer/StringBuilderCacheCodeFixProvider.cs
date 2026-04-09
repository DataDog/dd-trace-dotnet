// <copyright file="StringBuilderCacheCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer;

/// <summary>
/// Code fix provider that replaces <c>new StringBuilder()</c> with
/// <c>StringBuilderCache.Acquire()</c> and rewrites <c>.ToString()</c>
/// to <c>StringBuilderCache.GetStringAndRelease()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringBuilderCacheCodeFixProvider))]
[Shared]
public sealed class StringBuilderCacheCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use StringBuilderCache";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(Diagnostics.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(Document document, SyntaxNode creationNode, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        // Bail out for unsupported constructor overloads (e.g., (capacity, maxCapacity)) where
        // rewriting would silently change runtime semantics.
        if (AnalyzeConstructorArgs(creationNode, semanticModel, cancellationToken) is null)
        {
            return document;
        }

        var variableName = GetAssignedVariableName(creationNode);

        SyntaxNode newRoot;

        if (variableName is not null)
        {
            newRoot = ApplyVariableFix(root, creationNode, variableName, semanticModel, cancellationToken);
        }
        else
        {
            newRoot = ApplyInlineFix(root, creationNode, semanticModel, cancellationToken);
        }

        newRoot = AddUsingDirectiveIfMissing(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode ApplyVariableFix(SyntaxNode root, SyntaxNode creationNode, string variableName, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var enclosingFunction = GetEnclosingFunction(creationNode);

        // Collect all .ToString() invocations on the variable in the enclosing scope,
        // skipping nested lambdas/local functions that may shadow the variable name
        var toStringInvocations = ImmutableArray<InvocationExpressionSyntax>.Empty;
        if (enclosingFunction is not null)
        {
            toStringInvocations = enclosingFunction
                .DescendantNodes(descendIntoChildren: n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) || n == enclosingFunction)
                .OfType<InvocationExpressionSyntax>()
                .Where(inv =>
                    inv.Expression is MemberAccessExpressionSyntax memberAccess
                    && memberAccess.Name.Identifier.Text == "ToString"
                    && memberAccess.Expression is IdentifierNameSyntax id
                    && id.Identifier.Text == variableName
                    && inv.ArgumentList.Arguments.Count == 0)
                .ToImmutableArray();
        }

        if (toStringInvocations.Length <= 1)
        {
            // Single or zero .ToString() — replace creation + that one call
            return root.ReplaceNodes(
                toStringInvocations.Cast<SyntaxNode>().Append(creationNode),
                (original, _) =>
                {
                    if (original == creationNode)
                    {
                        return BuildAcquireCall(original, semanticModel, cancellationToken);
                    }

                    return BuildGetStringAndReleaseCall(variableName)
                        .WithTriviaFrom(original);
                });
        }

        // Multiple .ToString() calls — check for mutations between first and last
        if (enclosingFunction is not null && HasMutationsBetween(enclosingFunction, variableName, toStringInvocations.First(), toStringInvocations.Last()))
        {
            // Case B: mutations exist — only replace the last .ToString()
            return root.ReplaceNodes(
                new SyntaxNode[]
                {
                    creationNode,
                    toStringInvocations.Last(),
                },
                (original, _) =>
                {
                    if (original == creationNode)
                    {
                        return BuildAcquireCall(original, semanticModel, cancellationToken);
                    }

                    return BuildGetStringAndReleaseCall(variableName)
                        .WithTriviaFrom(original);
                });
        }

        // Case A: no mutations — use single GetStringAndRelease() + local variable
        return ApplyNoMutationFix(root, creationNode, variableName, toStringInvocations, semanticModel, cancellationToken);
    }

    private static SyntaxNode ApplyNoMutationFix(
        SyntaxNode root,
        SyntaxNode creationNode,
        string variableName,
        ImmutableArray<InvocationExpressionSyntax> toStringInvocations,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var resultVarName = variableName + "Result";

        // The first .ToString() becomes: var sbResult = StringBuilderCache.GetStringAndRelease(sb);
        // Remaining .ToString() calls become: sbResult
        var firstToString = toStringInvocations[0];

        // We need to:
        // 1. Replace the creation node with Acquire()
        // 2. Replace the first .ToString() statement with a GetStringAndRelease() + local var
        // 3. Replace remaining .ToString() calls with the result variable

        // Find the statement containing the first .ToString()
        var firstToStringStatement = firstToString.FirstAncestorOrSelf<StatementSyntax>();

        // Build the GetStringAndRelease local declaration:
        // var sbResult = StringBuilderCache.GetStringAndRelease(sb);
        var getStringAndReleaseStatement = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName("var"),
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(resultVarName)
                        .WithInitializer(
                            SyntaxFactory.EqualsValueClause(
                                BuildGetStringAndReleaseCall(variableName))))));

        // First pass: replace creation + all .ToString() calls
        var newRoot = root.ReplaceNodes(
            toStringInvocations.Cast<SyntaxNode>().Append(creationNode),
            (original, _) =>
            {
                if (original == creationNode)
                {
                    return BuildAcquireCall(original, semanticModel, cancellationToken);
                }

                // All .ToString() calls become the result variable reference
                return SyntaxFactory.IdentifierName(resultVarName)
                    .WithTriviaFrom(original);
            });

        // Second pass: insert the GetStringAndRelease statement before the first .ToString() statement
        // We need to find the updated version of the statement
        var updatedFirstToStringStatement = newRoot.DescendantNodes()
            .OfType<StatementSyntax>()
            .FirstOrDefault(s => s.Span.Start == firstToStringStatement!.Span.Start
                && s.Span.Length == firstToStringStatement.Span.Length);

        // Fallback: find statement containing the resultVarName identifier that was the first replacement
        if (updatedFirstToStringStatement is null)
        {
            // After ReplaceNodes, spans may shift. Find the first statement containing our result variable.
            var firstResultRef = newRoot.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .FirstOrDefault(id => id.Identifier.Text == resultVarName);

            updatedFirstToStringStatement = firstResultRef?.FirstAncestorOrSelf<StatementSyntax>();
        }

        if (updatedFirstToStringStatement?.Parent is BlockSyntax block)
        {
            var stmtIndex = block.Statements.IndexOf(updatedFirstToStringStatement);
            if (stmtIndex >= 0)
            {
                var releaseStmt = getStringAndReleaseStatement
                    .WithLeadingTrivia(updatedFirstToStringStatement.GetLeadingTrivia())
                    .WithTrailingTrivia(updatedFirstToStringStatement.GetTrailingTrivia())
                    .WithAdditionalAnnotations(Formatter.Annotation);

                var newStatements = block.Statements.Insert(stmtIndex, releaseStmt);
                var newBlock = block.WithStatements(newStatements);
                newRoot = newRoot.ReplaceNode(block, newBlock);
            }
        }

        return newRoot;
    }

    private static SyntaxNode ApplyInlineFix(SyntaxNode root, SyntaxNode creationNode, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // Walk up the fluent chain to find a trailing .ToString() call
        var toStringInvocation = FindChainedToString(creationNode);

        if (toStringInvocation is null)
        {
            // No .ToString() in the chain — just replace new StringBuilder() with Acquire()
            return root.ReplaceNode(creationNode, BuildAcquireCall(creationNode, semanticModel, cancellationToken));
        }

        // Replace the outer .ToString() invocation with GetStringAndRelease(<inner chain>)
        // and the creation node with Acquire() in one pass
        var memberAccess = (MemberAccessExpressionSyntax)toStringInvocation.Expression;
        var innerChain = memberAccess.Expression; // everything before .ToString()

        return root.ReplaceNodes(
            new SyntaxNode[]
            {
                creationNode,
                toStringInvocation,
            },
            (original, rewritten) =>
            {
                if (original == creationNode)
                {
                    return BuildAcquireCall(original, semanticModel, cancellationToken);
                }

                // This is the .ToString() invocation — wrap inner chain with GetStringAndRelease
                // At this point, the creationNode inside the chain has already been replaced with Acquire()
                // by ReplaceNodes, so we need to use the rewritten version of the inner chain
                var rewrittenInvocation = (InvocationExpressionSyntax)rewritten;
                var rewrittenMemberAccess = (MemberAccessExpressionSyntax)rewrittenInvocation.Expression;
                var rewrittenInnerChain = rewrittenMemberAccess.Expression;

                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("StringBuilderCache"),
                        SyntaxFactory.IdentifierName("GetStringAndRelease")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(rewrittenInnerChain))))
                    .WithTriviaFrom(original);
            });
    }

    private static InvocationExpressionSyntax? FindChainedToString(SyntaxNode creationNode)
    {
        // Walk up through the fluent method chain looking for .ToString()
        // Pattern: new StringBuilder().Append("x").ToString()
        // AST: InvocationExpression(MemberAccess(InvocationExpression(MemberAccess(ObjectCreation, Append)), ToString))
        for (var current = creationNode.Parent; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.Text == "ToString"
                && invocation.ArgumentList.Arguments.Count == 0)
            {
                return invocation;
            }

            // Only continue walking if we're still in a fluent chain
            if (current is not (MemberAccessExpressionSyntax or InvocationExpressionSyntax or ArgumentListSyntax))
            {
                break;
            }
        }

        return null;
    }

    private static bool HasMutationsBetween(
        SyntaxNode enclosingFunction,
        string variableName,
        InvocationExpressionSyntax firstToString,
        InvocationExpressionSyntax lastToString)
    {
        var firstSpanEnd = firstToString.Span.End;
        var lastSpanStart = lastToString.SpanStart;

        // Skip nested lambdas/local functions that may shadow the variable name
        foreach (var invocation in enclosingFunction.DescendantNodes(descendIntoChildren: n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) || n == enclosingFunction).OfType<InvocationExpressionSyntax>())
        {
            if (invocation.SpanStart <= firstSpanEnd || invocation.SpanStart >= lastSpanStart)
            {
                continue;
            }

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression is IdentifierNameSyntax id
                && id.Identifier.Text == variableName
                && memberAccess.Name.Identifier.Text != "ToString")
            {
                return true;
            }
        }

        // Also check for element access (sb[i] = ...)
        foreach (var elementAccess in enclosingFunction.DescendantNodes(descendIntoChildren: n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) || n == enclosingFunction).OfType<ElementAccessExpressionSyntax>())
        {
            if (elementAccess.SpanStart <= firstSpanEnd || elementAccess.SpanStart >= lastSpanStart)
            {
                continue;
            }

            if (elementAccess.Expression is IdentifierNameSyntax id
                && id.Identifier.Text == variableName)
            {
                return true;
            }
        }

        // Also check for assignment expressions targeting the variable or its properties/fields
        // e.g. sb.Length = 0 or sb = other
        foreach (var assignment in enclosingFunction.DescendantNodes(descendIntoChildren: n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) || n == enclosingFunction).OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.SpanStart <= firstSpanEnd || assignment.SpanStart >= lastSpanStart)
            {
                continue;
            }

            // sb = other  (reassignment of the variable itself)
            if (assignment.Left is IdentifierNameSyntax leftId
                && leftId.Identifier.Text == variableName)
            {
                return true;
            }

            // sb.Length = 0  (property/field assignment on the variable)
            if (assignment.Left is MemberAccessExpressionSyntax leftMember
                && leftMember.Expression is IdentifierNameSyntax memberId
                && memberId.Identifier.Text == variableName)
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionSyntax BuildAcquireCall(
        SyntaxNode creationNode,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var (capacityArg, appendArgs) = AnalyzeConstructorArgs(creationNode, semanticModel, cancellationToken)!.Value;

        var acquireAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("StringBuilderCache"),
            SyntaxFactory.IdentifierName("Acquire"));

        var acquireArgList = capacityArg is not null
            ? SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(capacityArg))
            : SyntaxFactory.ArgumentList();

        ExpressionSyntax result = SyntaxFactory.InvocationExpression(acquireAccess, acquireArgList);

        if (appendArgs.Length > 0)
        {
            // Chain .Append(value[, startIndex, length]) after Acquire()
            result = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    result,
                    SyntaxFactory.IdentifierName("Append")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(appendArgs)));
        }

        return result.WithTriviaFrom(creationNode);
    }

    private static (ArgumentSyntax? CapacityArg, ImmutableArray<ArgumentSyntax> AppendArgs)? AnalyzeConstructorArgs(
        SyntaxNode creationNode,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ArgumentListSyntax? argList = creationNode switch
        {
            ObjectCreationExpressionSyntax oc => oc.ArgumentList,
            ImplicitObjectCreationExpressionSyntax ic => ic.ArgumentList,
            _ => null,
        };

        if (argList is null || argList.Arguments.Count == 0)
        {
            return (null, ImmutableArray<ArgumentSyntax>.Empty);
        }

        var symbolInfo = semanticModel.GetSymbolInfo(creationNode, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol constructor)
        {
            // Can't resolve constructor — don't forward args to be safe
            return (null, ImmutableArray<ArgumentSyntax>.Empty);
        }

        var args = argList.Arguments;
        var paramCount = constructor.Parameters.Length;

        // StringBuilder(int capacity)
        if (paramCount == 1 && constructor.Parameters[0].Type.SpecialType == SpecialType.System_Int32)
        {
            return (args[0], ImmutableArray<ArgumentSyntax>.Empty);
        }

        // StringBuilder(string? value)
        if (paramCount == 1 && constructor.Parameters[0].Type.SpecialType == SpecialType.System_String)
        {
            return (null, ImmutableArray.Create(args[0]));
        }

        // StringBuilder(string? value, int capacity)
        if (paramCount == 2
            && constructor.Parameters[0].Type.SpecialType == SpecialType.System_String
            && constructor.Parameters[1].Type.SpecialType == SpecialType.System_Int32)
        {
            return (args[1], ImmutableArray.Create(args[0]));
        }

        // StringBuilder(int capacity, int maxCapacity)
        // StringBuilderCache.Acquire() has no maxCapacity parameter — rewriting would silently
        // drop the capacity bound and change runtime semantics, so skip the code fix for this overload.
        if (paramCount == 2
            && constructor.Parameters[0].Type.SpecialType == SpecialType.System_Int32
            && constructor.Parameters[1].Type.SpecialType == SpecialType.System_Int32)
        {
            return null;
        }

        // StringBuilder(string? value, int startIndex, int length, int capacity)
        if (paramCount == 4 && constructor.Parameters[0].Type.SpecialType == SpecialType.System_String)
        {
            return (args[3], ImmutableArray.Create(args[0], args[1], args[2]));
        }

        // Unknown overload — don't forward args
        return (null, ImmutableArray<ArgumentSyntax>.Empty);
    }

    private static InvocationExpressionSyntax BuildGetStringAndReleaseCall(string variableName)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("StringBuilderCache"),
                SyntaxFactory.IdentifierName("GetStringAndRelease")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(variableName)))));
    }

    private static string? GetAssignedVariableName(SyntaxNode creationNode)
    {
        var parent = creationNode.Parent;

        // var sb = new StringBuilder();
        if (parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            return declarator.Identifier.Text;
        }

        // sb = new StringBuilder();
        if (parent is AssignmentExpressionSyntax assignment
            && assignment.Left is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.Text;
        }

        return null;
    }

    private static SyntaxNode? GetEnclosingFunction(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                case AccessorDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }

    private static SyntaxNode AddUsingDirectiveIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        const string targetNamespace = "Datadog.Trace.Util";

        // Check if the using already exists
        var hasUsing = compilationUnit.Usings
            .Any(u => u.Name?.ToString() == targetNamespace);

        if (hasUsing)
        {
            return root;
        }

        // Match the line ending style of the existing using directives
        var existingTrailingTrivia = compilationUnit.Usings.LastOrDefault()?.GetTrailingTrivia();
        var trailingTrivia = existingTrailingTrivia?.Count > 0
            ? existingTrailingTrivia.Value
            : SyntaxFactory.TriviaList(SyntaxFactory.ElasticLineFeed);

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(targetNamespace))
            .WithTrailingTrivia(trailingTrivia)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return compilationUnit.AddUsings(usingDirective);
    }
}
