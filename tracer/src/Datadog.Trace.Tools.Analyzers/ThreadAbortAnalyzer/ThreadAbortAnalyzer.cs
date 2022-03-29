// <copyright file="ThreadAbortAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer
{
    /// <summary>
    /// An analyzer that tries to identify patterns that can introduce a risk of infinite loops
    /// In particular, it is designed to catch code vulnerable to an issue in the NETFramework
    /// runtime, as described here https://github.com/dotnet/runtime/issues/9633.
    ///
    /// The analyzer detects code similar to the following block. A ThreadAbortException is
    /// supposed to be special, in that it will always be rethrown after being caught. With
    /// the issue in the runtime, this doesn't happen. Therefore code like the following will
    /// never exit:
    ///
    /// <code>
    /// while(true)
    /// {
    ///   try {
    ///   } catch(Exception) {
    ///   }
    /// }
    /// </code>
    ///
    /// Instead, you must manually rethrow the exception:
    /// <code>
    /// while(true)
    /// {
    ///   try {
    ///   } catch(Exception) {
    ///     throw; // Required to avoid infinite recursion
    ///   }
    /// }
    /// </code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ThreadAbortAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID displayed in error messages
        /// </summary>
        public const string DiagnosticId = "ThreadAbortAnalyzer";

        private const string Category = "Reliability";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: Resources.Title,
            messageFormat: Resources.MessageFormat,
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Resources.Description);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyseSyntax, SyntaxKind.WhileStatement);
        }

        private void AnalyseSyntax(SyntaxNodeAnalysisContext context)
        {
            var whileStatement = context.Node as WhileStatementSyntax;
            if (whileStatement is null)
            {
                if (!Debugger.IsAttached)
                {
                    Debugger.Launch();
                }

                return;
            }

            var problematicCatch = ThreadAbortSyntaxHelper.FindProblematicCatchClause(whileStatement, context.SemanticModel);

            if (problematicCatch is null)
            {
                // no issues
                return;
            }

            // For all such symbols, produce a diagnostic.
            var diagnostic = Diagnostic.Create(Rule, problematicCatch.GetLocation());

            context.ReportDiagnostic(diagnostic);
        }
    }
}
