// <copyright file="DuckTypeNullCheckAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer
{
    /// <summary>
    /// Checks fo r null checks against IDuckType instances.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DuckTypeNullCheckAnalyzer : DiagnosticAnalyzer
    {
        private const string DatadogIDuckTypeInterface = "Datadog.Trace.DuckTyping.IDuckType";

        /// <summary>
        /// We exclude certain namespaces from this rule:
        /// - Activity because we do a log of "as" pattern matching against various runtime implementations and expect null for some
        /// </summary>
        private static readonly ImmutableArray<string> ExcludedNamespacePrefixes =
            ImmutableArray.Create(
                "Datadog.Trace.Activity");

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
                if (duckType is null)
                {
                    return;
                }

                compilationContext.RegisterOperationAction(
                    ctx => AnalyzeBinaryNullCheck(ctx, duckType),
                    OperationKind.BinaryOperator);

                compilationContext.RegisterOperationAction(
                    ctx => AnalyzeIsPatternNullCheck(ctx, duckType),
                    OperationKind.IsPattern);
            });
        }

        private static void AnalyzeBinaryNullCheck(OperationAnalysisContext context, INamedTypeSymbol duckType)
        {
            // if (duckType == null) or if (duckType != null) is what we looking for (operands can be swapped)
            // both are technically incorrect
            var bin = (IBinaryOperation)context.Operation;

            // make sure it is == or !=
            if (bin.OperatorKind != BinaryOperatorKind.Equals &&
                bin.OperatorKind != BinaryOperatorKind.NotEquals)
            {
                return;
            }

            // find which side we need, really unsure if anyone has ever written null == duckType :)
            var leftIsNull = IsNullLiteral(bin.LeftOperand);
            var rightIsNull = IsNullLiteral(bin.RightOperand);
            if (!leftIsNull && !rightIsNull)
            {
                return;
            }

            // Look at the non-null side and unwrap casts/boxing to object/dynamic
            // candidate here is: ConversionOperation Type: object
            // it is an implicit cast / box to object (unlike the `is null` pattern)
            // so we need to undo that before we can check if it is a IDuckType
            var candidate = leftIsNull ? bin.RightOperand : bin.LeftOperand;

            // When we have == or != null it seems that the candidate.Type is just an Object, we need to get the DuckType from it
            // we can query the SemanticModel to get the actual type, but that proved to be very slow
            // so we can unwrap it instead
            var type = UnwrapForType(candidate);

            if (type is null || !ImplementsDuckType(type, duckType) || IsExcluded(type))
            {
                return;
            }

            Report(context);
        }

        private static void AnalyzeIsPatternNullCheck(OperationAnalysisContext context, INamedTypeSymbol duckType)
        {
            var isPattern = (IIsPatternOperation)context.Operation;

            if (!IsNullPattern(isPattern.Pattern))
            {
                return;
            }

            // this just falls through to the default op case as it isn't boxed / casted to object
            var type = UnwrapForType(isPattern.Value);
            if (type is null || !ImplementsDuckType(type, duckType) || IsExcluded(type))
            {
                return;
            }

            Report(context);
        }

        private static void Report(OperationAnalysisContext context)
        {
            var diagnostic = Diagnostic.Create(
                DuckDiagnostics.ADuckIsNeverNullRule,
                context.Operation.Syntax.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private static ITypeSymbol? UnwrapForType(IOperation op)
        {
            op = Unwrap(op);

            // The type here will be the actual type like Datadog.Trace.DuckTyping.IDuckType
            return op.Type;
        }

        private static IOperation Unwrap(IOperation op)
        {
            while (true)
            {
                switch (op)
                {
                    case IConversionOperation c when c.IsImplicit:
                        // implicit meaning that there wasn't a (object) cast
                        // this happens automatically in the `==` and `!=` operators
                        // c.Operand here is something like => ParameterReferenceOperation Type: Datadog.Trace.DuckTyping.IDuckType
                        op = c.Operand;
                        continue;

                    case IConversionOperation c
                        when c.Type is { SpecialType: SpecialType.System_Object } ||
                             c.Type is { TypeKind: TypeKind.Dynamic }:
                        // Explicit cast/as to object or dynamic — unwrap so we can see the original type
                        op = c.Operand;
                        continue;

                    default:
                        return op;
                }
            }
        }

        private static bool ImplementsDuckType(ITypeSymbol type, INamedTypeSymbol duckType)
        {
            // where T : IDuckType
            // where T : IFoo, IDuckType
            // where T : IFoo
            // IFoo : IDuckType (in different file)
            if (type is ITypeParameterSymbol tp)
            {
                foreach (var c in tp.ConstraintTypes)
                {
                    if (ImplementsDuckType(c, duckType))
                    {
                        return true;
                    }
                }

                return false;
            }

            // NOTE: IDuckType? == IDuckType which surprised me
            if (SymbolEqualityComparer.Default.Equals(type, duckType))
            {
                return true;
            }

            // IFoo : IBar
            // IBar : IDuckType
            // NOTE: IFoo : IBar (where IBar implements IDuckType) is handled by the AllInterfaces check below
            foreach (var i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, duckType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExcluded(ITypeSymbol type)
        {
            var ns = GetNamespace(type);
            if (ns is null)
            {
                return false;
            }

            foreach (var prefix in ExcludedNamespacePrefixes)
            {
                if (ns.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? GetNamespace(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol tp)
            {
                foreach (var c in tp.ConstraintTypes)
                {
                    var nam = GetNamespace(c);
                    if (nam is not null)
                    {
                        return nam;
                    }
                }

                return null;
            }

            // Avoid allocations from "global::"
            var ns = type.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (ns is null)
            {
                return null;
            }

            return ns.StartsWith("global::", System.StringComparison.Ordinal) ? ns.Substring(8) : ns;
        }

        private static bool IsNullLiteral(IOperation operation)
        {
            var op = Unwrap(operation);
            if (op is ILiteralOperation lit && lit.ConstantValue.HasValue)
            {
                return lit.ConstantValue.Value is null;
            }

            return false;
        }

        private static bool IsNullPattern(IPatternOperation pattern)
        {
            // is null
            if (pattern is IConstantPatternOperation cp && IsNullLiteral(cp.Value))
            {
                return true;
            }

            // is not null => Negated(Constant(null))
            if (pattern is INegatedPatternOperation neg &&
                neg.Pattern is IConstantPatternOperation cp2 &&
                IsNullLiteral(cp2.Value))
            {
                return true;
            }

            return false;
        }
    }
}
