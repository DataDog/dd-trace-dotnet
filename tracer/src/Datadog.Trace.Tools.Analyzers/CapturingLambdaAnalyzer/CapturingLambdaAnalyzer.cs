// <copyright file="CapturingLambdaAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.CapturingLambdaAnalyzer;

/// <summary>
/// Detects lambdas passed to Task.Run, Task.Factory.StartNew, and .ContinueWith
/// that capture variables from the enclosing scope, causing closure allocations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CapturingLambdaAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.CapturingLambdaRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var taskType = compilationContext.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskFactoryType = compilationContext.Compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskFactory");

            if (taskType is null)
            {
                return;
            }

            var targetTypes = new TargetTypes(taskType, taskFactoryType);

            compilationContext.RegisterSyntaxNodeAction(
                c => AnalyzeInvocation(c, in targetTypes),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, in TargetTypes targetTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic bail-out: must be a member access with a matching method name
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("Run" or "StartNew" or "ContinueWith"))
        {
            return;
        }

        // Resolve the method symbol
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        // Verify the method belongs to Task, Task<T>, or TaskFactory
        if (!IsTargetMethod(methodSymbol, methodName, targetTypes))
        {
            return;
        }

        // Find lambda/anonymous method arguments
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var expression = argument.Expression;

            if (expression is not (LambdaExpressionSyntax or AnonymousMethodExpressionSyntax))
            {
                continue;
            }

            // Static lambdas cannot capture
            if (expression is LambdaExpressionSyntax lambda && lambda.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            // Analyze data flow to detect captured variables
            var body = GetLambdaBody(expression);
            if (body is null)
            {
                continue;
            }

            var dataFlow = context.SemanticModel.AnalyzeDataFlow(body);
            if (dataFlow is null || !dataFlow.Succeeded)
            {
                continue;
            }

            if (dataFlow.Captured.IsEmpty)
            {
                continue;
            }

            var capturedNames = string.Join(", ", dataFlow.Captured.Select(s => s.Name));

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.CapturingLambdaRule,
                    expression.GetLocation(),
                    methodName,
                    capturedNames));

            // Only report once per invocation (the first lambda argument)
            break;
        }
    }

    private static bool IsTargetMethod(IMethodSymbol method, string methodName, in TargetTypes targetTypes)
    {
        var containingType = method.ContainingType?.OriginalDefinition;
        if (containingType is null)
        {
            return false;
        }

        return methodName switch
        {
            "Run" => IsTaskType(containingType, targetTypes.Task),
            "ContinueWith" => IsTaskType(containingType, targetTypes.Task),
            "StartNew" => targetTypes.TaskFactory is not null
                          && SymbolEqualityComparer.Default.Equals(containingType, targetTypes.TaskFactory),
            _ => false,
        };
    }

    /// <summary>
    /// Checks if <paramref name="type"/> is <see cref="System.Threading.Tasks.Task"/>
    /// or derives from it (e.g. <see cref="System.Threading.Tasks.Task{TResult}"/>).
    /// </summary>
    private static bool IsTaskType(INamedTypeSymbol type, INamedTypeSymbol taskType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType?.OriginalDefinition)
        {
            if (SymbolEqualityComparer.Default.Equals(current, taskType))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode? GetLambdaBody(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedLambdaExpressionSyntax p => (SyntaxNode?)p.Block ?? p.ExpressionBody,
            SimpleLambdaExpressionSyntax s => (SyntaxNode?)s.Block ?? s.ExpressionBody,
            AnonymousMethodExpressionSyntax a => a.Block,
            _ => null,
        };
    }

    private readonly struct TargetTypes
    {
        public readonly INamedTypeSymbol Task;
        public readonly INamedTypeSymbol? TaskFactory;

        public TargetTypes(INamedTypeSymbol task, INamedTypeSymbol? taskFactory)
        {
            Task = task;
            TaskFactory = taskFactory;
        }
    }
}
