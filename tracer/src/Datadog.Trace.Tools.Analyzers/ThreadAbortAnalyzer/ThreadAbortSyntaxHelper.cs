// <copyright file="ThreadAbortSyntaxHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer
{
    internal static class ThreadAbortSyntaxHelper
    {
        public static CatchClauseSyntax FindProblematicCatchClause(WhileStatementSyntax whileStatement, SemanticModel model)
        {
            var blockSyntax = whileStatement.Statement as BlockSyntax;
            if (blockSyntax is null)
            {
                return null;
            }

            var innerStatements = blockSyntax.Statements;
            if (innerStatements.Count != 1)
            {
                // only applies when try directly nested under while and only child
                return null;
            }

            var tryCatchStatement = innerStatements[0] as TryStatementSyntax;
            if (tryCatchStatement is null)
            {
                // Not a try catch nested in a while
                return null;
            }

            CatchClauseSyntax catchClause = null;
            var willCatchThreadAbort = false;
            var willRethrowThreadAbort = false;

            foreach (var catchSyntax in tryCatchStatement.Catches)
            {
                catchClause = catchSyntax;
                var exceptionTypeSyntax = catchSyntax.Declaration.Type;
                if (CanCatchThreadAbort(exceptionTypeSyntax, model))
                {
                    willCatchThreadAbort = true;

                    // We're in the catch block that will catch the ThreadAbort
                    // Make sure that we re-throw the exception
                    // This is a very basic check, in that it doesn't check control flow etc
                    // It requires that you have a throw; in the catch block

                    // We are only checking the direct ancestors (nesting breaks this analysis)
                    // and if you have an expression, it must be the exception declared in the
                    willRethrowThreadAbort = catchSyntax.Block.Statements
                        .OfType<ThrowStatementSyntax>()
                        .Any();
                    break;
                }
            }

            if (willCatchThreadAbort && !willRethrowThreadAbort)
            {
                return catchClause;
            }

            return null;
        }

        private static bool CanCatchThreadAbort(TypeSyntax syntax, SemanticModel model)
        {
            var exceptionType = model.GetSymbolInfo(syntax).Symbol as INamedTypeSymbol;
            var exceptionTypeName = exceptionType?.ToString();
            return exceptionTypeName == typeof(ThreadAbortException).FullName
                || exceptionTypeName == typeof(SystemException).FullName
                || exceptionTypeName == typeof(Exception).FullName;
        }
    }
}
