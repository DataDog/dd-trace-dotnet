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

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers
{
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

            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation
             || memberAccess.Name.Identifier.ValueText != "AsString"
             || invocation.ArgumentList.Arguments.Count != 0)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use AsRedactedString",
                    createChangedDocument: cancellationToken => UseRedactedStringAsync(context.Document, invocation, memberAccess, cancellationToken),
                    equivalenceKey: nameof(ConfigurationBuilderWithKeysCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> UseRedactedStringAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var redactedName = SyntaxFactory.IdentifierName("AsRedactedString").WithTriviaFrom(memberAccess.Name);
            var redactedInvocation = invocation.WithExpression(memberAccess.WithName(redactedName));
            return document.WithSyntaxRoot(root!.ReplaceNode(invocation, redactedInvocation));
        }
    }
}
