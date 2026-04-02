// <copyright file="EnvironmentRestorerAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Datadog.Trace.Tools.Analyzers.TestingAnalyzer;

/// <summary>
/// DD0006: Detects Environment.SetEnvironmentVariable calls in xUnit test methods
/// that are not covered by an [EnvironmentRestorer] attribute.
///
/// DD0013: Detects redundant [EnvironmentRestorer] attributes — either duplicated
/// across methods (should be class-level) or already covered by a class-level attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnvironmentRestorerAnalyzer : DiagnosticAnalyzer
{
    private const string EnvironmentRestorerAttributeName = "EnvironmentRestorerAttribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        Diagnostics.MissingEnvironmentRestorerRule,
        Diagnostics.MissingEnvironmentRestorerNonConstantRule,
        Diagnostics.RedundantEnvironmentRestorerRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Resolve Xunit.FactAttribute — if not found, this isn't a test project
            var factAttributeType = compilationContext.Compilation.GetTypeByMetadataName("Xunit.FactAttribute");
            if (factAttributeType is null)
            {
                return;
            }

            // DD0006: flag SetEnvironmentVariable without [EnvironmentRestorer]
            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, factAttributeType),
                SyntaxKind.InvocationExpression);

            // DD0013: flag redundant [EnvironmentRestorer] attributes
            compilationContext.RegisterSymbolAction(
                ctx => AnalyzeNamedType(ctx),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol factAttributeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic fast-path: is it a .SetEnvironmentVariable(...) call?
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.Text != "SetEnvironmentVariable")
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return;
        }

        // Semantic check: is it System.Environment.SetEnvironmentVariable?
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method
            || method.ContainingType?.ToString() != "System.Environment")
        {
            return;
        }

        // Find the containing method declaration
        var containingMethod = GetContainingMethodSyntax(invocation);
        if (containingMethod is null)
        {
            return;
        }

        // Get the method symbol
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod, context.CancellationToken);
        if (methodSymbol is not IMethodSymbol testMethodSymbol)
        {
            return;
        }

        // Check if the containing method is a test method (has attribute deriving from FactAttribute)
        if (!IsTestMethod(testMethodSymbol, factAttributeType))
        {
            return;
        }

        // Try to resolve the variable name as a compile-time constant
        var constantValue = context.SemanticModel.GetConstantValue(arguments[0].Expression, context.CancellationToken);
        string? variableName = constantValue is { HasValue: true, Value: string s } ? s : null;

        if (variableName is not null)
        {
            // Constant name: check if covered by [EnvironmentRestorer] at method or class level
            if (HasEnvironmentRestorerForVariable(testMethodSymbol.GetAttributes(), variableName)
                || HasEnvironmentRestorerForVariable(testMethodSymbol.ContainingType?.GetAttributes() ?? ImmutableArray<AttributeData>.Empty, variableName))
            {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(Diagnostics.MissingEnvironmentRestorerRule, invocation.GetLocation(), variableName));
        }
        else
        {
            // Non-constant name: always flag, [EnvironmentRestorer] cannot suppress
            context.ReportDiagnostic(
                Diagnostic.Create(Diagnostics.MissingEnvironmentRestorerNonConstantRule, invocation.GetLocation()));
        }
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        // Collect class-level [EnvironmentRestorer] variable names
        var classVariables = GetEnvironmentRestorerVariables(namedType.GetAttributes());

        // Collect method-level [EnvironmentRestorer] attributes with their variables and locations
        var methodVariableOccurrences = new Dictionary<string, List<Location>>();

        foreach (var member in namedType.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            foreach (var attr in methodSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name != EnvironmentRestorerAttributeName)
                {
                    continue;
                }

                var variables = GetVariablesFromAttribute(attr);
                foreach (var variable in variables)
                {
                    // Check if this variable is already covered by class-level attribute
                    if (classVariables.Contains(variable))
                    {
                        // Redundant: method-level is already covered by class-level
                        var location = attr.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                        if (location is not null)
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    Diagnostics.RedundantEnvironmentRestorerRule,
                                    location,
                                    $"[EnvironmentRestorer(\"{variable}\")] on method '{methodSymbol.Name}' is already covered by the class-level attribute — remove it"));
                        }
                    }
                    else
                    {
                        // Track for duplicate detection
                        if (!methodVariableOccurrences.TryGetValue(variable, out var locations))
                        {
                            locations = new List<Location>();
                            methodVariableOccurrences[variable] = locations;
                        }

                        var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                        if (attrLocation is not null)
                        {
                            locations.Add(attrLocation);
                        }
                    }
                }
            }
        }

        // Report duplicates across methods
        foreach (var kvp in methodVariableOccurrences)
        {
            if (kvp.Value.Count >= 2)
            {
                foreach (var location in kvp.Value)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Diagnostics.RedundantEnvironmentRestorerRule,
                            location,
                            $"[EnvironmentRestorer(\"{kvp.Key}\")] appears on multiple methods — move it to the class level instead"));
                }
            }
        }
    }

    private static MethodDeclarationSyntax? GetContainingMethodSyntax(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is MethodDeclarationSyntax method)
            {
                return method;
            }

            // Stop at type/namespace boundaries
            if (current is TypeDeclarationSyntax or NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
            {
                break;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool IsTestMethod(IMethodSymbol method, INamedTypeSymbol factAttributeType)
    {
        foreach (var attr in method.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            while (attrClass is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(attrClass, factAttributeType))
                {
                    return true;
                }

                attrClass = attrClass.BaseType;
            }
        }

        return false;
    }

    private static bool HasEnvironmentRestorerForVariable(ImmutableArray<AttributeData> attributes, string variableName)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != EnvironmentRestorerAttributeName)
            {
                continue;
            }

            var variables = GetVariablesFromAttribute(attr);
            if (variables.Contains(variableName))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> GetEnvironmentRestorerVariables(ImmutableArray<AttributeData> attributes)
    {
        var result = new HashSet<string>();
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.Name != EnvironmentRestorerAttributeName)
            {
                continue;
            }

            foreach (var variable in GetVariablesFromAttribute(attr))
            {
                result.Add(variable);
            }
        }

        return result;
    }

    private static List<string> GetVariablesFromAttribute(AttributeData attr)
    {
        var result = new List<string>();
        foreach (var arg in attr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Array)
            {
                foreach (var element in arg.Values)
                {
                    if (element.Value is string s)
                    {
                        result.Add(s);
                    }
                }
            }
            else if (arg.Value is string s)
            {
                result.Add(s);
            }
        }

        return result;
    }
}
