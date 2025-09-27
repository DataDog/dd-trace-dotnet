// <copyright file="DuckTypeNullCheckAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer
{
    /// <summary>
    /// Checks for null checks against IDuckType instances.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DuckTypeNullCheckAnalyzer : DiagnosticAnalyzer
    {
        private const string DatadogIDuckTypeInterface = "Datadog.Trace.DuckTyping.IDuckType";

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(DuckDiagnostics.ADuckIsNeverNullRule);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            // not checking any generated code
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(static compilationContext =>
            {
                var duckType = compilationContext.Compilation.GetTypeByMetadataName(DatadogIDuckTypeInterface);

                if (duckType == null)
                {
                    return;
                }

                compilationContext.RegisterSyntaxNodeAction(ctx => AnalyzeEquals((BinaryExpressionSyntax)ctx.Node, duckType, ctx), SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
                compilationContext.RegisterSyntaxNodeAction(ctx => AnalyzeIsPattern((IsPatternExpressionSyntax)ctx.Node, duckType, ctx), SyntaxKind.IsPatternExpression);
            });
        }

        private static void AnalyzeEquals(BinaryExpressionSyntax binaryExpression, INamedTypeSymbol duckType, SyntaxNodeAnalysisContext ctx)
        {
            if (!(IsNull(binaryExpression.Left) || IsNull(binaryExpression.Right)))
            {
                return;
            }

            var candidate = IsNull(binaryExpression.Left) ? binaryExpression.Right : binaryExpression.Left;
            if (!IsDuck(candidate, duckType, ctx.SemanticModel, ctx.CancellationToken))
            {
                return;
            }

            ctx.ReportDiagnostic(Diagnostic.Create(DuckDiagnostics.ADuckIsNeverNullRule, binaryExpression.GetLocation()));
        }

        private static void AnalyzeIsPattern(IsPatternExpressionSyntax isPattern, INamedTypeSymbol duckType, SyntaxNodeAnalysisContext ctx)
        {
            if (!IsNullPattern(isPattern.Pattern))
            {
                return;
            }

            var expression = isPattern.Expression;

            if (!IsDuck(expression, duckType, ctx.SemanticModel, ctx.CancellationToken))
            {
                return;
            }

            ctx.ReportDiagnostic(Diagnostic.Create(DuckDiagnostics.ADuckIsNeverNullRule, isPattern.GetLocation()));
        }

        private static bool IsNull(ExpressionSyntax expression) =>
            expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } ||
            (expression is ParenthesizedExpressionSyntax p && IsNull(p.Expression));

        private static bool IsNullPattern(PatternSyntax pattern) =>
            pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } ||
            (pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } neg &&
            neg.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } });

        // this goes through and attempts to extract the type out
        // we then pass this in to check if it implements IDuckType
        private static bool IsDuck(
            ExpressionSyntax expression,
            INamedTypeSymbol? duckType,
            SemanticModel semanticModel,
            CancellationToken token)
        {
            // ((object)duckType) casting causes issues so remove the parens
            expression = StripOuterParentheses(expression);
            if (expression is CastExpressionSyntax cast && IsObjectTypeSyntax(cast.Type))
            {
                expression = StripOuterParentheses(cast.Expression);
            }

            var operation = semanticModel.GetOperation(expression, token);

            if (operation is IConversionOperation conv)
            {
                operation = conv.Operand;
            }

            var type = operation?.Type;

            if (type is null)
            {
                var info = semanticModel.GetTypeInfo(expression, token);
                type = info.Type ?? info.ConvertedType;
            }

            // both Type and ConvertedType can still be null so check again just to be safe
            if (type is null)
            {
                return false;
            }

            // handle any constraints
            if (type is ITypeParameterSymbol tp)
            {
                return tp.ConstraintTypes.Any(t => IsTheTypeAnIDuckType(t, duckType));
            }

            return IsTheTypeAnIDuckType(type, duckType);
        }

        private static bool IsTheTypeAnIDuckType(ITypeSymbol type, INamedTypeSymbol? duckType)
        {
            return duckType is not null
                && (SymbolEqualityComparer.Default.Equals(type, duckType)
                || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, duckType)));
        }

        private static ExpressionSyntax StripOuterParentheses(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax p)
            {
                expression = p.Expression;
            }

            return expression;
        }

        // this seems seems to be that in VS when you open a code file the operands get boxed(?) to "object"
        // if we don't account for it, you see the errors at compile time, in the error list, but then disappear
        // when you open the code file in the editor.
        private static bool IsObjectTypeSyntax(TypeSyntax t) =>
            t is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword }
            || (t is IdentifierNameSyntax id && id.Identifier.ValueText == "object");
    }
}
