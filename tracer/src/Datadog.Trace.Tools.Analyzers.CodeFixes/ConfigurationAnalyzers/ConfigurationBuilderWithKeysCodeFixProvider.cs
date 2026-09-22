// <copyright file="ConfigurationBuilderWithKeysCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers
{
    /// <summary>
    /// Provides fixes that replace configuration accessors with redacted equivalents.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConfigurationBuilderWithKeysCodeFixProvider))]
    [Shared]
    public class ConfigurationBuilderWithKeysCodeFixProvider : CodeFixProvider
    {
        /// <inheritdoc />
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ["DD0015"];

        /// <inheritdoc />
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc />
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            while (node is not null and not InvocationExpressionSyntax)
            {
                node = node.Parent;
            }

            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel?.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
             || !TryGetRedactedInvocation(invocation, memberAccess, operation, out var redactedInvocation, out var redactedAccessor))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Use {redactedAccessor}",
                    createChangedDocument: cancellationToken => UseRedactedAccessorAsync(context.Document, invocation, redactedInvocation, cancellationToken),
                    equivalenceKey: nameof(ConfigurationBuilderWithKeysCodeFixProvider)),
                diagnostic);
        }

        private static bool TryGetRedactedInvocation(
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess,
            IInvocationOperation operation,
            out InvocationExpressionSyntax redactedInvocation,
            out string redactedAccessor)
        {
            if (operation.TargetMethod.IsStatic
             || operation.Instance?.Type is not { } instanceType
             || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, instanceType))
            {
                redactedInvocation = null!;
                redactedAccessor = null!;
                return false;
            }

            switch (operation.TargetMethod.Name)
            {
                case "AsString" when operation.TargetMethod.Parameters.Length == 0
                                       || (operation.TargetMethod.Parameters.Length == 1
                                        && operation.TargetMethod.Parameters[0].Type.SpecialType == SpecialType.System_String):
                    redactedAccessor = "AsRedactedString";
                    redactedInvocation = RenameAccessor(invocation, memberAccess, redactedAccessor);
                    return true;

                case "AsStringResult":
                    redactedAccessor = "AsRedactedStringResult";
                    var stringArguments = invocation.ArgumentList.Arguments;
                    foreach (var argument in operation.Arguments)
                    {
                        if (argument.Parameter?.Name == "recordValue" && argument.Syntax is ArgumentSyntax argumentSyntax)
                        {
                            if (argument.Value.ConstantValue is not { HasValue: true, Value: true })
                            {
                                redactedInvocation = null!;
                                redactedAccessor = null!;
                                return false;
                            }

                            stringArguments = stringArguments.Remove(argumentSyntax);
                            break;
                        }
                    }

                    redactedInvocation = RenameAccessor(invocation.WithArgumentList(invocation.ArgumentList.WithArguments(stringArguments)), memberAccess, redactedAccessor);
                    return true;

                case "AsDictionaryResult":
                    redactedAccessor = "AsRedactedDictionaryResult";
                    var dictionaryArguments = invocation.ArgumentList.Arguments;
                    var hasSeparator = false;
                    foreach (var argument in operation.Arguments)
                    {
                        if (argument.Parameter?.Name == "separator")
                        {
                            hasSeparator = true;
                        }
                        else if (argument.Parameter?.Name == "allowOptionalMappings"
                              && argument.Value.ConstantValue is { HasValue: true, Value: false }
                              && argument.Syntax is ArgumentSyntax optionalMappingsSyntax)
                        {
                            dictionaryArguments = dictionaryArguments.Remove(optionalMappingsSyntax);
                        }
                        else
                        {
                            redactedInvocation = null!;
                            redactedAccessor = null!;
                            return false;
                        }
                    }

                    if (!hasSeparator)
                    {
                        var separator = SyntaxFactory.Argument(
                            nameColon: SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("separator"))
                                                     .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)),
                            refKindKeyword: default,
                            expression: SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(':')));
                        dictionaryArguments = dictionaryArguments.Add(separator);
                    }

                    redactedInvocation = RenameAccessor(invocation.WithArgumentList(invocation.ArgumentList.WithArguments(dictionaryArguments)), memberAccess, redactedAccessor);
                    return true;

                default:
                    redactedInvocation = null!;
                    redactedAccessor = null!;
                    return false;
            }
        }

        private static InvocationExpressionSyntax RenameAccessor(
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess,
            string redactedAccessor)
        {
            var redactedName = SyntaxFactory.IdentifierName(redactedAccessor).WithTriviaFrom(memberAccess.Name);
            return invocation.WithExpression(memberAccess.WithName(redactedName));
        }

        private static async Task<Document> UseRedactedAccessorAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            InvocationExpressionSyntax redactedInvocation,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return document.WithSyntaxRoot(root!.ReplaceNode(invocation, redactedInvocation));
        }
    }
}
