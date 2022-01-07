// <copyright file="InstrumentationDefinitionsGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions;

/// <inheritdoc />
[Generator]
public class InstrumentationDefinitionsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source

        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations =
            context.SyntaxProvider.CreateSyntaxProvider(IsAttributedClass, GetPotentialClassesForGeneration)
                   .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
            context.CompilationProvider.Combine(classDeclarations.Collect());

        IncrementalValueProvider<(IReadOnlyList<CallTargetDefinitionSource> Definitions, IReadOnlyList<Diagnostic> Diagnostics)> detailsToRender =
            compilationAndClasses.Select(static (x, ct) => GetDefinitionToWrite(x.Item1, x.Item2, ct));

        context.RegisterSourceOutput(detailsToRender, static (spc, source) =>
                                         Execute(source.Definitions, source.Diagnostics, spc));
    }

    private static bool IsAttributedClass(SyntaxNode node, CancellationToken cancellationToken)
        => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0;

    private static ClassDeclarationSyntax? GetPotentialClassesForGeneration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                IMethodSymbol? attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
                if (attributeSymbol is null)
                {
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName == Constants.InstrumentAttribute)
                {
                    return classDeclarationSyntax;
                }
            }
        }

        return null;
    }

    private static (IReadOnlyList<CallTargetDefinitionSource> Definitions, IReadOnlyList<Diagnostic> Diagnostics) GetDefinitionToWrite(
        Compilation compilation,
        ImmutableArray<ClassDeclarationSyntax> classes,
        CancellationToken ct)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return (Array.Empty<CallTargetDefinitionSource>(), Array.Empty<Diagnostic>());
        }

        var (definitions, diagnostics) = GetCallTargetDefinitionSources(
            compilation,
            classes.Distinct(),
            ct);

        return (definitions, diagnostics);
    }

    private static void Execute(
        IReadOnlyList<CallTargetDefinitionSource> definitions,
        IReadOnlyList<Diagnostic> diagnostics,
        SourceProductionContext context)
    {
        if (definitions.Count != 0)
        {
            string source = Sources.CreateCallTargetDefinitions(definitions);
            context.AddSource("InstrumentationDefinitions.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static (IReadOnlyList<CallTargetDefinitionSource> Definitions, IReadOnlyList<Diagnostic> Diagnostics) GetCallTargetDefinitionSources(
        Compilation compilation,
        IEnumerable<ClassDeclarationSyntax> classes,
        CancellationToken cancellationToken)
    {
        INamedTypeSymbol? instrumentAttribute = compilation.GetTypeByMetadataName(Constants.InstrumentAttribute);
        if (instrumentAttribute is null)
        {
            // nothing to do if this type isn't available
            return (Array.Empty<CallTargetDefinitionSource>(), Array.Empty<Diagnostic>());
        }

        var results = new List<CallTargetDefinitionSource>();
        List<Diagnostic>? diagnostics = null;

        foreach (ClassDeclarationSyntax classDec in classes)
        {
            // stop if we're asked to
            cancellationToken.ThrowIfCancellationRequested();

            SemanticModel sm = compilation.GetSemanticModel(classDec.SyntaxTree);
            INamedTypeSymbol? classSymbol = sm.GetDeclaredSymbol(classDec, cancellationToken) as INamedTypeSymbol;
            Debug.Assert(classSymbol is not null, "Instrumented class is not present");
            var boundAttributes = classSymbol!.GetAttributes();

            foreach (AttributeData attributeData in boundAttributes)
            {
                var hasMisconfiguredInput = false;

                if (!instrumentAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                string? assemblyName = null;
                string[]? assemblyNames = null;
                string? integrationName = null;
                string? typeName = null;
                string? methodName = null;
                string? returnTypeName = null;
                string? minimumVersion = null;
                string? maximumVersion = null;
                string[]? parameterTypeNames = null;
                Type? callTargetType = null;
                int? integrationType = null;

                foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
                {
                    if (namedArgument.Value.Kind == TypedConstantKind.Error)
                    {
                        hasMisconfiguredInput = true;
                        break;
                    }

                    switch (namedArgument.Key)
                    {
                        case nameof(Constants.Properties.AssemblyName):
                            assemblyName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.IntegrationName):
                            integrationName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.TypeName):
                            typeName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.MethodName):
                            methodName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.ReturnTypeName):
                            returnTypeName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.MinimumVersion):
                            minimumVersion = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.MaximumVersion):
                            maximumVersion = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.Properties.ParameterTypeNames):
                            parameterTypeNames = GetStringArray(namedArgument.Value.Values);
                            break;
                        case nameof(Constants.Properties.CallTargetType):
                            callTargetType = namedArgument.Value.Value as Type;
                            break;
                        case nameof(Constants.Properties.CallTargetIntegrationType):
                            integrationType = namedArgument.Value.Value as int?;
                            break;
                        default:
                            hasMisconfiguredInput = true;
                            break;
                    }

                    if (hasMisconfiguredInput)
                    {
                        break;
                    }
                }

                if (hasMisconfiguredInput)
                {
                    continue;
                }

                if (assemblyNames is null or { Length: 0 } && assemblyName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.AssemblyName,
                            Constants.Properties.AssemblyNames));
                }

                if (typeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.TypeName));
                }

                if (integrationName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.IntegrationName));
                }

                if (methodName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.MethodName));
                }

                if (returnTypeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.MethodName));
                }

                (ushort Major, ushort Minor, ushort Patch) minVersion = default;
                (ushort Major, ushort Minor, ushort Patch) maxVersion = default;
                if (minimumVersion is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.MinimumVersion));
                }
                else if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        InvalidVersionFormatDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax()));
                }

                if (maximumVersion is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.Properties.MaximumVersion));
                }
                else if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        InvalidVersionFormatDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax()));
                }

                if (hasMisconfiguredInput)
                {
                    continue;
                }

                foreach (var assembly in assemblyNames ?? new[] { assemblyName })
                {
                    foreach (var type in typeNames ?? new[] { typeName })
                    {
                        results.Add(
                            new CallTargetDefinitionSource(
                                integrationName: integrationName!,
                                assemblyName: assembly!,
                                targetTypeName: type!,
                                targetMethodName: methodName!,
                                targetReturnType: returnTypeName!,
                                targetParameterTypes: parameterTypeNames ?? Array.Empty<string>(),
                                minimumVersion: minVersion,
                                maximumVersion: maxVersion,
                                instrumentationTypeName: (callTargetType?.ToString() ?? classSymbol.ToDisplayString()),
                                integrationType: integrationType ?? 0));
                    }
                }
            }
        }

        return (results, diagnostics as IReadOnlyList<Diagnostic> ?? Array.Empty<Diagnostic>());
    }

    private static string GetClassNamespace(ClassDeclarationSyntax classDec)
    {
        string? nameSpace;

        // determine the namespace the class is declared in, if any
        SyntaxNode? potentialNamespaceParent = classDec.Parent;
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
            && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            nameSpace = namespaceParent.Name.ToString();
            while (true)
            {
                if (namespaceParent.Parent is NamespaceDeclarationSyntax parent)
                {
                    namespaceParent = parent;
                    nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                }
                else
                {
                    return nameSpace;
                }
            }
        }

        return string.Empty;
    }

    private static bool TryGetVersion(string version, ushort defaultValue, out (ushort Major, ushort Minor, ushort Patch) parsedVersion)
    {
        var major = defaultValue;
        var minor = defaultValue;
        var patch = defaultValue;

        var parts = version.Split('.');
        if (parts.Length >= 1 && !ParsePart(parts[0], defaultValue, out major))
        {
            parsedVersion = default;
            return false;
        }

        if (parts.Length >= 2 && !ParsePart(parts[1], defaultValue, out minor))
        {
            parsedVersion = default;
            return false;
        }

        if (parts.Length >= 3 && !ParsePart(parts[2], defaultValue, out patch))
        {
            parsedVersion = default;
            return false;
        }

        parsedVersion = (major, minor, patch);
        return true;

        static bool ParsePart(string part, ushort defaultValue, out ushort value)
        {
            if (part == "*")
            {
                value = defaultValue;
                return true;
            }

            if (ushort.TryParse(part, out value))
            {
                return true;
            }

            value = defaultValue;
            return false;
        }
    }

    private static string[]? GetStringArray(ImmutableArray<TypedConstant>? maybeParamValues)
    {
        if (!maybeParamValues.HasValue)
        {
            return null;
        }

        var paramValues = maybeParamValues.Value;
        var values = new string[paramValues.Length];
        for (int i = 0; i < paramValues.Length; i++)
        {
            values[i] = paramValues[i].Value?.ToString() ?? string.Empty;
        }

        return values;
    }

    internal readonly struct PropertyTag
    {
        public readonly bool IsReadOnly;
        public readonly string PropertyName;
        public readonly string TagValue;

        public PropertyTag(bool isReadOnly, string propertyName, string tagValue)
        {
            IsReadOnly = isReadOnly;
            PropertyName = propertyName;
            TagValue = tagValue;
        }
    }
}
