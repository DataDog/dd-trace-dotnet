// <copyright file="StringCaseInterceptorGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.StringCaseInterception;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// On .NET Framework only, intercepts every <see cref="string.ToUpperInvariant()"/>/
/// <see cref="string.ToLowerInvariant()"/> call site in the compilation and redirects it to
/// System.StringUtil, which avoids the allocation those BCL methods
/// always incur on that TFM when no character actually needs to change case. See
/// System.StringUtil for why the helper itself must never be rewritten
/// by this generator (it would recurse infinitely).
/// </summary>
[Generator]
public class StringCaseInterceptorGenerator : IIncrementalGenerator
{
    private const string ToUpperInvariant = "ToUpperInvariant";
    private const string ToLowerInvariant = "ToLowerInvariant";
    private const string SkipAttributeFullName = "Datadog.Trace.Util.SkipStringCaseInterceptionAttribute";
    private const string HelperFullName = "System.StringUtil";
    private const string InterceptorsFullName = "Datadog.Trace.Generated.Interceptors.StringCaseInterceptors";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var isNetFramework =
            context.AnalyzerConfigOptionsProvider
                   .Select(static (provider, _) =>
                               provider.GlobalOptions.TryGetValue("build_property.TargetFrameworkIdentifier", out var tfi)
                            && tfi == ".NETFramework")
                   .WithTrackingName(TrackingNames.StringCaseIsNetFramework);

        IncrementalValuesProvider<CallSite> callSites =
            context.SyntaxProvider
                   .CreateSyntaxProvider(
                        predicate: static (node, _) => IsCandidate(node),
                        transform: static (ctx, ct) => GetCallSite(ctx, ct))
                   .Where(static x => x is not null)
                   .Select(static (x, _) => x!)
                   .WithTrackingName(TrackingNames.StringCaseCallSites);

        IncrementalValueProvider<(ImmutableArray<CallSite> CallSites, bool IsNetFramework)> combined =
            callSites.Collect()
                     .Combine(isNetFramework)
                     .WithTrackingName(TrackingNames.StringCaseCombined);

        context.RegisterSourceOutput(combined, static (spc, source) => Execute(source.CallSites, source.IsNetFramework, spc));
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation)
        {
            return false;
        }

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            _ => null,
        };

        if (name is not (ToUpperInvariant or ToLowerInvariant))
        {
            return false;
        }

        // we add a .NET Framework check in here to avoid the expensive GetCallSite calls from running at all on .NET Core
        if (node.SyntaxTree.Options is not CSharpParseOptions options)
        {
            return false;
        }

        foreach (var symbol in options.PreprocessorSymbolNames)
        {
            if (symbol == "NETFRAMEWORK")
            {
                return true;
            }
        }

        return false;
    }

    private static CallSite? GetCallSite(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol { IsStatic: false, Parameters.Length: 0 } method)
        {
            return null;
        }

        if (method.ContainingType?.SpecialType != SpecialType.System_String)
        {
            return null;
        }

        var methodName = method.Name;
        if (methodName is not (ToUpperInvariant or ToLowerInvariant))
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        if (IsExcluded(semanticModel, invocation, ct))
        {
            return null;
        }

        var location = semanticModel.GetInterceptableLocation(invocation, ct);
        if (location is null)
        {
            return null;
        }

        return new CallSite(methodName, location.GetInterceptsLocationAttributeSyntax());
    }

    /// <summary>
    /// Opts a call site out when the containing method/type carries <see cref="SkipAttributeFullName"/>,
    /// or when the call is inside the helper or the interceptor stub themselves
    /// </summary>
    private static bool IsExcluded(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken ct)
    {
        var enclosingSymbol = semanticModel.GetEnclosingSymbol(invocation.SpanStart, ct);
        if (enclosingSymbol is null)
        {
            return false;
        }

        if (HasSkipAttribute(enclosingSymbol))
        {
            return true;
        }

        for (var type = enclosingSymbol.ContainingType; type is not null; type = type.ContainingType)
        {
            var fullName = type.ToDisplayString();
            if (fullName is HelperFullName or InterceptorsFullName)
            {
                return true;
            }

            if (HasSkipAttribute(type))
            {
                return true;
            }
        }

        return false;

        static bool HasSkipAttribute(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == SkipAttributeFullName)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static void Execute(ImmutableArray<CallSite> callSites, bool isNetFramework, SourceProductionContext context)
    {
        if (!isNetFramework || callSites.IsDefaultOrEmpty)
        {
            return;
        }

        var upper = new List<string>();
        var lower = new List<string>();

        foreach (var callSite in callSites)
        {
            (callSite.MethodName == ToUpperInvariant ? upper : lower).Add(callSite.InterceptsLocationAttribute);
        }

        upper.Sort(StringComparer.Ordinal);
        lower.Sort(StringComparer.Ordinal);

        var source = Sources.GenerateInterceptors(upper, lower);
        context.AddSource("StringCaseInterceptors.g.cs", SourceText.From(source, Encoding.UTF8));
    }
}
