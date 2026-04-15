// <copyright file="RemoveNumericToStringCodeFixProvider.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// Code fix that removes unnecessary .ToString() calls on numeric types in log arguments
/// and updates explicit generic type arguments to match.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveNumericToStringCodeFixProvider))]
[Shared]
public sealed class RemoveNumericToStringCodeFixProvider : CodeFixProvider
{
    private const string Title = "Remove unnecessary .ToString() call";

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Diagnostics.NumericToStringInLogDiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];

        if (FindToStringInvocation(root, diagnostic) is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => RemoveToStringAsync(context.Document, diagnostic, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> RemoveToStringAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var toStringInvocation = FindToStringInvocation(root, diagnostic);

        if (toStringInvocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var receiverExpression = memberAccess.Expression;

        if (!diagnostic.Properties.TryGetValue("ReceiverTypeName", out var numericKeyword)
            || numericKeyword is null)
        {
            root = root.ReplaceNode(toStringInvocation, receiverExpression.WithTriviaFrom(toStringInvocation));
            return document.WithSyntaxRoot(root);
        }

        // Find the enclosing log method invocation
        var logInvocation = toStringInvocation.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (logInvocation is null)
        {
            root = root.ReplaceNode(toStringInvocation, receiverExpression.WithTriviaFrom(toStringInvocation));
            return document.WithSyntaxRoot(root);
        }

        // Get the method symbol to understand the type arguments
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var methodSymbol = semanticModel?.GetSymbolInfo(logInvocation, cancellationToken).Symbol as IMethodSymbol;

        if (methodSymbol is null || !methodSymbol.IsGenericMethod)
        {
            root = root.ReplaceNode(toStringInvocation, receiverExpression.WithTriviaFrom(toStringInvocation));
            return document.WithSyntaxRoot(root);
        }

        // Find which type argument index corresponds to this argument
        var messageTemplateParamIndex = -1;
        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            if (methodSymbol.Parameters[i].Name == "messageTemplate")
            {
                messageTemplateParamIndex = i;
                break;
            }
        }

        var argumentList = logInvocation.ArgumentList;
        var argIndex = -1;
        for (var i = 0; i < argumentList.Arguments.Count; i++)
        {
            if (argumentList.Arguments[i].Expression == toStringInvocation)
            {
                argIndex = i;
                break;
            }
        }

        var typeArgIndex = argIndex - messageTemplateParamIndex - 1;

        if (messageTemplateParamIndex < 0 || typeArgIndex < 0 || typeArgIndex >= methodSymbol.TypeArguments.Length)
        {
            root = root.ReplaceNode(toStringInvocation, receiverExpression.WithTriviaFrom(toStringInvocation));
            return document.WithSyntaxRoot(root);
        }

        // Check if the log call already has explicit generic type arguments
        if (logInvocation.Expression is MemberAccessExpressionSyntax logMemberAccess
            && logMemberAccess.Name is GenericNameSyntax existingGenericName)
        {
            // Preserve existing type arg syntax nodes, only replace the one being fixed
            var existingTypeArgs = existingGenericName.TypeArgumentList.Arguments;
            var newTypeArgs = new TypeSyntax[existingTypeArgs.Count];
            for (var i = 0; i < existingTypeArgs.Count; i++)
            {
                newTypeArgs[i] = i == typeArgIndex
                    ? SyntaxFactory.ParseTypeName(numericKeyword).WithTriviaFrom(existingTypeArgs[i])
                    : existingTypeArgs[i];
            }

            var newTypeArgList = SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(newTypeArgs))
                .WithTriviaFrom(existingGenericName.TypeArgumentList);

            var newGenericName = existingGenericName.WithTypeArgumentList(newTypeArgList);

            var nodesToReplace = new SyntaxNode[]
            {
                existingGenericName,
                toStringInvocation,
            };
            root = root.ReplaceNodes(
                nodesToReplace,
                (original, _) =>
                {
                    if (original == existingGenericName)
                    {
                        return newGenericName;
                    }

                    return receiverExpression.WithTriviaFrom(toStringInvocation);
                });
        }
        else if (logInvocation.Expression is MemberAccessExpressionSyntax simpleMemberAccess
                 && simpleMemberAccess.Name is IdentifierNameSyntax identifierName)
        {
            // No explicit generic type args — build from method symbol and add them
            var typeArgs = methodSymbol.TypeArguments
                .Select((t, i) => i == typeArgIndex ? numericKeyword : GetTypeKeyword(t))
                .Select(keyword => SyntaxFactory.ParseTypeName(keyword))
                .ToArray();

            var newTypeArgList = SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(typeArgs));

            var newGenericName = SyntaxFactory.GenericName(identifierName.Identifier, newTypeArgList);

            var newLogExpression = simpleMemberAccess.WithName(newGenericName);

            var nodesToReplace = new SyntaxNode[]
            {
                simpleMemberAccess,
                toStringInvocation,
            };
            root = root.ReplaceNodes(
                nodesToReplace,
                (original, _) =>
                {
                    if (original == simpleMemberAccess)
                    {
                        return newLogExpression;
                    }

                    return receiverExpression.WithTriviaFrom(toStringInvocation);
                });
        }
        else
        {
            root = root.ReplaceNode(toStringInvocation, receiverExpression.WithTriviaFrom(toStringInvocation));
        }

        return document.WithSyntaxRoot(root);
    }

    private static string GetTypeKeyword(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Boolean => "bool",
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
            SpecialType.System_String => "string",
            SpecialType.System_Object => "object",
            SpecialType.System_IntPtr => "nint",
            SpecialType.System_UIntPtr => "nuint",
            _ => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        };
    }

    private static InvocationExpressionSyntax? FindToStringInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // FindNode may return the ArgumentSyntax wrapping the invocation,
        // so search descendants for the .ToString() invocation
        return node
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(
                inv => inv.Expression is MemberAccessExpressionSyntax ma
                    && ma.Name.Identifier.Text == "ToString");
    }
}
