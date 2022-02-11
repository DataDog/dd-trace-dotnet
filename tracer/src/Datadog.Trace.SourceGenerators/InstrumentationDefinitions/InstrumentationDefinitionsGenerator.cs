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
            context.SyntaxProvider.CreateSyntaxProvider(
                        static (node, token) => IsAttributedClass(node, token),
                        static (syntaxContext, token) => GetPotentialClassesForGeneration(syntaxContext, token))
                   .Where(static m => m is not null)!;

        IncrementalValuesProvider<AttributeData> assemblyAttributes =
            context.CompilationProvider.SelectMany(static (compilation, _) => compilation.Assembly.GetAttributes());

        IncrementalValuesProvider<AttributeData> adoNetAssemblyAttributes =
            assemblyAttributes.Where(static a => IsAssemblyAttributeForGeneration(a));

        IncrementalValueProvider<((Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) Left, ImmutableArray<AttributeData> Right)> compilationAndClasses =
            context.CompilationProvider
                   .Combine(classDeclarations.Collect())
                   .Combine(adoNetAssemblyAttributes.Collect());

        IncrementalValueProvider<(IReadOnlyList<CallTargetDefinitionSource> Definitions, IReadOnlyList<Diagnostic> Diagnostics)> detailsToRender =
            compilationAndClasses.Select(static (x, ct) => GetDefinitionToWrite(x.Left.Left, x.Left.Right, x.Right, ct));

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

                if (fullName == Constants.InstrumentAttribute || fullName == Constants.AdoNetTargetSignatureAttribute)
                {
                    return classDeclarationSyntax;
                }
            }
        }

        return null;
    }

    private static bool IsAssemblyAttributeForGeneration(AttributeData attributeData)
    {
        return attributeData.AttributeClass?.ToDisplayString() == Constants.AdoNetInstrumentAttribute;
    }

    private static (IReadOnlyList<CallTargetDefinitionSource> Definitions, IReadOnlyList<Diagnostic> Diagnostics) GetDefinitionToWrite(
        Compilation compilation,
        ImmutableArray<ClassDeclarationSyntax> classes,
        ImmutableArray<AttributeData> assemblyAttributes,
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
            assemblyAttributes,
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
        ImmutableArray<AttributeData> assemblyAttributes,
        CancellationToken cancellationToken)
    {
        INamedTypeSymbol? instrumentAttribute = compilation.GetTypeByMetadataName(Constants.InstrumentAttribute);
        if (instrumentAttribute is null)
        {
            // nothing to do if this type isn't available
            return (Array.Empty<CallTargetDefinitionSource>(), Array.Empty<Diagnostic>());
        }

        INamedTypeSymbol? adoNetSignatureAttribute = compilation.GetTypeByMetadataName(Constants.AdoNetTargetSignatureSymbolName);
        if (adoNetSignatureAttribute is null && !assemblyAttributes.IsDefaultOrEmpty)
        {
            // nothing to do if this type isn't available
            return (Array.Empty<CallTargetDefinitionSource>(), Array.Empty<Diagnostic>());
        }

        INamedTypeSymbol? adoNetInstrumentationAttribute = compilation.GetTypeByMetadataName(Constants.AdoNetInstrumentAttribute);
        if (adoNetInstrumentationAttribute is null && !assemblyAttributes.IsDefaultOrEmpty)
        {
            // nothing to do if this type isn't available
            return (Array.Empty<CallTargetDefinitionSource>(), Array.Empty<Diagnostic>());
        }

        var results = new List<CallTargetDefinitionSource>();
        var signatures = assemblyAttributes.IsDefaultOrEmpty ? null : new Dictionary<string, AdoNetSignature>();
        List<Diagnostic>? diagnostics = null;

        foreach (ClassDeclarationSyntax classDec in classes)
        {
            // stop if we're asked to
            cancellationToken.ThrowIfCancellationRequested();

            SemanticModel sm = compilation.GetSemanticModel(classDec.SyntaxTree);
            INamedTypeSymbol? classSymbol = sm.GetDeclaredSymbol(classDec, cancellationToken) as INamedTypeSymbol;
            Debug.Assert(classSymbol is not null, "Instrumented class is not present");
            var boundAttributes = classSymbol!.GetAttributes();

            // Process InstrumentMethodAttribute first
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
                string[]? typeNames = null;
                string? methodName = null;
                string? returnTypeName = null;
                string? minimumVersion = null;
                string? maximumVersion = null;
                string[]? parameterTypeNames = null;
                string? callTargetType = null;
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
                        case nameof(Constants.InstrumentAttributeProperties.AssemblyName):
                            assemblyName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.AssemblyNames):
                            assemblyNames = GetStringArray(namedArgument.Value.Values);
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.IntegrationName):
                            integrationName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.TypeName):
                            typeName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.TypeNames):
                            typeNames = GetStringArray(namedArgument.Value.Values);
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.MethodName):
                            methodName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.ReturnTypeName):
                            returnTypeName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.MinimumVersion):
                            minimumVersion = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.MaximumVersion):
                            maximumVersion = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.ParameterTypeNames):
                            parameterTypeNames = GetStringArray(namedArgument.Value.Values);
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.CallTargetType):
                            callTargetType = (namedArgument.Value.Value as INamedTypeSymbol)?.ToDisplayString();
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.CallTargetIntegrationType):
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
                            Constants.InstrumentAttributeProperties.AssemblyName,
                            Constants.InstrumentAttributeProperties.AssemblyNames));
                }

                if (typeNames is null or { Length: 0 } && typeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.TypeName,
                            Constants.InstrumentAttributeProperties.TypeNames));
                }

                if (integrationName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.IntegrationName));
                }

                if (methodName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MethodName));
                }

                if (returnTypeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.ReturnTypeName));
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
                            Constants.InstrumentAttributeProperties.MinimumVersion));
                }
                else if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        InvalidVersionFormatDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MinimumVersion));
                }

                if (maximumVersion is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MaximumVersion));
                }
                else if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        InvalidVersionFormatDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MaximumVersion));
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
                                instrumentationTypeName: (callTargetType ?? classSymbol.ToDisplayString()),
                                integrationType: integrationType ?? 0,
                                isAdoNetIntegration: false));
                    }
                }
            }

            // no need to extract signature attribute details, as we don't have any assembly attributes to use them
            if (assemblyAttributes.IsDefaultOrEmpty)
            {
                continue;
            }

            // Process AdoNetSignatureAttribute next
            foreach (AttributeData attributeData in boundAttributes)
            {
                var hasMisconfiguredInput = false;

                if (!adoNetSignatureAttribute!.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                string? methodName = null;
                string? returnTypeName = null;
                string[]? parameterTypeNames = null;
                int? integrationType = null;
                int? returnType = null;
                string? callTargetType = null;

                foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
                {
                    if (namedArgument.Value.Kind == TypedConstantKind.Error)
                    {
                        hasMisconfiguredInput = true;
                        break;
                    }

                    switch (namedArgument.Key)
                    {
                        case nameof(Constants.AdoNetSignatureAttributeProperties.MethodName):
                            methodName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.AdoNetSignatureAttributeProperties.ReturnTypeName):
                            returnTypeName = namedArgument.Value.Value?.ToString();
                            break;
                        case nameof(Constants.AdoNetSignatureAttributeProperties.ParameterTypeNames):
                            parameterTypeNames = GetStringArray(namedArgument.Value.Values);
                            break;
                        case nameof(Constants.AdoNetSignatureAttributeProperties.CallTargetType):
                            callTargetType = (namedArgument.Value.Value as INamedTypeSymbol)?.ToDisplayString();
                            break;
                        case nameof(Constants.AdoNetSignatureAttributeProperties.CallTargetIntegrationType):
                            integrationType = namedArgument.Value.Value as int?;
                            break;
                        case nameof(Constants.AdoNetSignatureAttributeProperties.ReturnType):
                            returnType = namedArgument.Value.Value as int?;
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

                if (methodName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.AdoNetSignatureAttributeProperties.MethodName));
                }

                if (callTargetType is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.AdoNetSignatureAttributeProperties.CallTargetType));
                }

                if (returnType is 0 && returnTypeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.AdoNetSignatureAttributeProperties.ReturnTypeName,
                            Constants.AdoNetSignatureAttributeProperties.ReturnType));
                }

                if (hasMisconfiguredInput)
                {
                    continue;
                }

                signatures!.Add(
                    classSymbol.ToDisplayString(),
                    new AdoNetSignature(
                        targetMethodName: methodName!,
                        targetReturnType: returnTypeName,
                        targetParameterTypes: parameterTypeNames ?? Array.Empty<string>(),
                        instrumentationTypeName: callTargetType!.ToString(),
                        callTargetIntegrationType: integrationType ?? 0,
                        returnType: returnType ?? 0));
            }
        }

        // Now build the adonet references
        foreach (AttributeData attributeData in assemblyAttributes)
        {
            var hasMisconfiguredInput = false;

            if (!adoNetInstrumentationAttribute!.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
            {
                continue;
            }

            string? assemblyName = null;
            string? integrationName = null;
            string? typeName = null;
            string? minimumVersion = null;
            string? maximumVersion = null;
            string? dataReaderTypeName = null;
            string? dataReaderTaskTypeName = null;
            string[]? signatureAttributeTypes = null;

            foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.Value.Kind == TypedConstantKind.Error)
                {
                    hasMisconfiguredInput = true;
                    break;
                }

                switch (namedArgument.Key)
                {
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.AssemblyName):
                        assemblyName = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.TypeName):
                        typeName = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.MinimumVersion):
                        minimumVersion = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.MaximumVersion):
                        maximumVersion = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.IntegrationName):
                        integrationName = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.DataReaderType):
                        dataReaderTypeName = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.DataReaderTaskType):
                        dataReaderTaskTypeName = namedArgument.Value.Value?.ToString();
                        break;
                    case nameof(Constants.AdoNetInstrumentAttributeProperties.TargetMethodAttributes):
                        signatureAttributeTypes = GetTypeArray(namedArgument.Value.Values);
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

            if (string.IsNullOrEmpty(assemblyName))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.AssemblyName));
            }

            if (string.IsNullOrEmpty(typeName))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.TypeName));
            }

            if (integrationName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.IntegrationName));
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
                        Constants.AdoNetInstrumentAttributeProperties.MinimumVersion));
            }
            else if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    InvalidVersionFormatDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.MinimumVersion));
            }

            if (maximumVersion is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.MaximumVersion));
            }
            else if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    InvalidVersionFormatDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.MaximumVersion));
            }

            if (dataReaderTypeName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.DataReaderType));
            }

            if (dataReaderTaskTypeName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.DataReaderTaskType));
            }

            if (signatureAttributeTypes is null or { Length: 0 })
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new List<Diagnostic>();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetInstrumentAttributeProperties.TargetMethodAttributes));
            }

            if (hasMisconfiguredInput)
            {
                continue;
            }

            foreach (var signatureAttributeName in signatureAttributeTypes!)
            {
                if (!signatures!.TryGetValue(signatureAttributeName, out var signatureAttribute))
                {
                    diagnostics ??= new List<Diagnostic>();
                    diagnostics.Add(
                        UnknownAdoNetSignatureNameDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            signatureAttributeName));
                    continue;
                }

                var returnTypeName = signatureAttribute.ReturnType switch
                {
                    1 => dataReaderTypeName,
                    2 => dataReaderTaskTypeName,
                    _ => signatureAttribute.TargetReturnType
                };

                results.Add(
                    new CallTargetDefinitionSource(
                        integrationName: integrationName!,
                        assemblyName: assemblyName!,
                        targetTypeName: typeName!,
                        targetMethodName: signatureAttribute.TargetMethodName,
                        targetReturnType: returnTypeName!,
                        targetParameterTypes: signatureAttribute.TargetParameterTypes,
                        minimumVersion: minVersion,
                        maximumVersion: maxVersion,
                        instrumentationTypeName: signatureAttribute.InstrumentationTypeName,
                        integrationType: signatureAttribute.CallTargetIntegrationType,
                        isAdoNetIntegration: true));
            }
        }

        return (results, diagnostics as IReadOnlyList<Diagnostic> ?? Array.Empty<Diagnostic>());
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

    private static string[]? GetTypeArray(ImmutableArray<TypedConstant>? maybeParamValues)
    {
        if (!maybeParamValues.HasValue)
        {
            return null;
        }

        var paramValues = maybeParamValues.Value;
        var values = new string[paramValues.Length];
        for (int i = 0; i < paramValues.Length; i++)
        {
            values[i] = (paramValues[i].Value as INamedTypeSymbol)?.ToDisplayString() ?? string.Empty;
        }

        return values;
    }
}
