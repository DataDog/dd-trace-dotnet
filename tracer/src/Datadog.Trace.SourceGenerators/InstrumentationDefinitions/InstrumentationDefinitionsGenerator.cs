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
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.InstrumentationDefinitions;
using Datadog.Trace.SourceGenerators.InstrumentationDefinitions.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <inheritdoc />
[Generator]
public class InstrumentationDefinitionsGenerator : IIncrementalGenerator
{
    private const string InstrumentedMethodAttribute = "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute";
    private const string AdoNetInstrumentAttribute = "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute";
    private const string AdoNetTargetSignatureAttribute = AdoNetInstrumentAttribute + ".AdoNetTargetSignatureAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all the [InstrumentMethod] instances on classes
        IncrementalValuesProvider<Result<EquatableArray<CallTargetDefinitionSource>>> callTargetDefinitions =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                InstrumentedMethodAttribute,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetCallTargetDefinitionSources(ctx, ct))
                   .WithTrackingName(TrackingNames.CallTargetDefinitionSource);

        // Get all the `[AdoNetTargetSignature] attributes inside the AdoNetClientInstrumentMethodsAttribute type
        IncrementalValuesProvider<Result<EquatableArray<(string ClassName, AdoNetSignature Signature)>>> adoNetSignatures =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        AdoNetInstrumentAttribute + "+AdoNetTargetSignatureAttribute", // metadata name uses `+`
                        predicate: static (node, _) => node is ClassDeclarationSyntax,
                        transform: static (ctx, ct) => GetAdoNetSignatures(ctx, ct))
                   .WithTrackingName(TrackingNames.AdoNetSignatures);

        // Get all the [AdoNetClientInstrumentMethods]  assembly attributes
        IncrementalValuesProvider<Result<EquatableArray<AssemblyCallTargetDefinitionSource>>> assemblyCallTargetDefinitions =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        AdoNetInstrumentAttribute,
                        predicate: static (node, _) => node is CompilationUnitSyntax,
                        transform: static (ctx, ct) => GetAssemblyCallTargetDefinitionSources(ctx, ct))
                   .WithTrackingName(TrackingNames.AssemblyCallTargetDefinitionSource);

        // merge the adonet signatures
        IncrementalValueProvider<ImmutableArray<(string ClassName, AdoNetSignature Signature)>> allSignatures =
            adoNetSignatures
               .SelectMany(static (result, _) => result.Value)
               .Collect();

        IncrementalValuesProvider<Result<CallTargetDefinitionSource?>> adoNetCallTargetDefinitions =
            assemblyCallTargetDefinitions
               .SelectMany(static (result, _) => result.Value)
               .Combine(allSignatures)
               .Select((tuple, _) => MergeAdoNetAttributes(tuple.Left, tuple.Right))
               .WithTrackingName(TrackingNames.AdoNetCallTargetDefinitionSource);

        context.ReportDiagnostics(
            callTargetDefinitions
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.CallTargetDiagnostics));

        context.ReportDiagnostics(
            adoNetSignatures
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.AdoNetDiagnostics));

        context.ReportDiagnostics(
            assemblyCallTargetDefinitions
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.AssemblyDiagnostics));

        context.ReportDiagnostics(
            adoNetCallTargetDefinitions
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.AdoNetMergeDiagnostics));

        var allCallTargetDefinitions =
            callTargetDefinitions
               .SelectMany(static (x, _) => x.Value)
               .Collect();

        var allAdoNetDefinitions =
            adoNetCallTargetDefinitions
               .Where(x => x.Value is not null)
               .Select((x, _) => x.Value!)
               .Collect();

        context.RegisterSourceOutput(
            allCallTargetDefinitions.Combine(allAdoNetDefinitions),
            static (spc, source) =>
                Execute(source.Left, source.Right, spc));
    }

    private static Result<EquatableArray<CallTargetDefinitionSource>> GetCallTargetDefinitionSources(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        INamedTypeSymbol? classSymbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (classSymbol is null)
        {
            // nothing to do if this type isn't available
            return new Result<EquatableArray<CallTargetDefinitionSource>>(default, default);
        }

        ct.ThrowIfCancellationRequested();

        List<DiagnosticInfo>? diagnostics = null;
        List<CallTargetDefinitionSource>? results = null;

        // Process InstrumentMethodAttribute first
        // Iterate over the GeneratorAttributeSyntaxContext.Attributes property which is pre-populated with the targeted attributes
        foreach (AttributeData attributeData in ctx.Attributes)
        {
            if ((attributeData.AttributeClass?.Name == "InstrumentMethodAttribute" ||
                 attributeData.AttributeClass?.Name == "InstrumentMethod")
             && attributeData.AttributeClass.ToDisplayString() == InstrumentedMethodAttribute)
            {
                var hasMisconfiguredInput = false;
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
                int? integrationKind = null;
                var instrumentationCategory = InstrumentationCategory.Tracing;

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
                        case nameof(Constants.InstrumentAttributeProperties.CallTargetIntegrationKind):
                            integrationKind = namedArgument.Value.Value as int?;
                            break;
                        case nameof(Constants.InstrumentAttributeProperties.InstrumentationCategory):
                            instrumentationCategory = (InstrumentationCategory)(namedArgument.Value.Value as int?).GetValueOrDefault();
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
                    diagnostics ??= new();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.AssemblyName,
                            Constants.InstrumentAttributeProperties.AssemblyNames));
                }

                if (typeNames is null or { Length: 0 } && typeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.TypeName,
                            Constants.InstrumentAttributeProperties.TypeNames));
                }

                if (integrationName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.IntegrationName));
                }

                if (methodName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MethodName));
                }

                if (returnTypeName is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
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
                    diagnostics ??= new();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MinimumVersion));
                }
                else if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
                    diagnostics.Add(
                        InvalidVersionFormatDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MinimumVersion));
                }

                if (maximumVersion is null)
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
                    diagnostics.Add(
                        MissingRequiredPropertyDiagnostic.Create(
                            attributeData.ApplicationSyntaxReference?.GetSyntax(),
                            Constants.InstrumentAttributeProperties.MaximumVersion));
                }
                else if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
                {
                    hasMisconfiguredInput = true;
                    diagnostics ??= new();
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
                        results ??= new();
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
                                instrumentationTypeName: callTargetType ?? classSymbol.ToDisplayString(),
                                integrationKind: integrationKind ?? 0,
                                isAdoNetIntegration: false,
                                instrumentationCategory: instrumentationCategory));
                    }
                }
            }
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        return new Result<EquatableArray<CallTargetDefinitionSource>>(results is null ? default : new(results.ToArray()), errors);
    }

    private static Result<EquatableArray<(string ClassName, AdoNetSignature Signature)>> GetAdoNetSignatures(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        INamedTypeSymbol? classSymbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (classSymbol is null)
        {
            // nothing to do if this type isn't available
            return new Result<EquatableArray<(string ClassName, AdoNetSignature Signature)>>(default, default);
        }

        ct.ThrowIfCancellationRequested();

        List<DiagnosticInfo>? diagnostics = null;
        List<(string ClassName, AdoNetSignature Signature)>? results = null;

        // Process AdoNetSignatureAttribute next
        // Iterate over the GeneratorAttributeSyntaxContext.Attributes property which is pre-populated with the targeted attributes
        foreach (AttributeData attributeData in ctx.Attributes)
        {
            var hasMisconfiguredInput = false;

            string? methodName = null;
            string? returnTypeName = null;
            string[]? parameterTypeNames = null;
            int? integrationKind = null;
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
                    case nameof(Constants.AdoNetSignatureAttributeProperties.CallTargetIntegrationKind):
                        integrationKind = namedArgument.Value.Value as int?;
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
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetSignatureAttributeProperties.MethodName));
            }

            if (callTargetType is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        attributeData.ApplicationSyntaxReference?.GetSyntax(),
                        Constants.AdoNetSignatureAttributeProperties.CallTargetType));
            }

            if (returnType is 0 && returnTypeName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
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

            results ??= new();
            results!.Add(
                (
                    classSymbol.ToDisplayString(),
                    new AdoNetSignature(
                        targetMethodName: methodName!,
                        targetReturnType: returnTypeName,
                        targetParameterTypes: parameterTypeNames ?? Array.Empty<string>(),
                        instrumentationTypeName: callTargetType!.ToString(),
                        callTargetIntegrationKind: integrationKind ?? 0,
                        returnType: returnType ?? 0)));
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        return new Result<EquatableArray<(string, AdoNetSignature)>>(results is null ? default : new(results.ToArray()), errors);
    }

    private static Result<EquatableArray<AssemblyCallTargetDefinitionSource>> GetAssemblyCallTargetDefinitionSources(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        List<DiagnosticInfo>? diagnostics = null;
        List<AssemblyCallTargetDefinitionSource>? results = null;

        // Now build the adonet references
        // Iterate over the GeneratorAttributeSyntaxContext.Attributes property which is pre-populated with the targeted attributes
        foreach (AttributeData attributeData in ctx.Attributes)
        {
            var hasMisconfiguredInput = false;

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

            var syntaxNode = attributeData.ApplicationSyntaxReference?.GetSyntax();

            if (string.IsNullOrEmpty(assemblyName))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.AssemblyName));
            }

            if (string.IsNullOrEmpty(typeName))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.TypeName));
            }

            if (integrationName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.IntegrationName));
            }

            (ushort Major, ushort Minor, ushort Patch) minVersion = default;
            (ushort Major, ushort Minor, ushort Patch) maxVersion = default;
            if (minimumVersion is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.MinimumVersion));
            }
            else if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    InvalidVersionFormatDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.MinimumVersion));
            }

            if (maximumVersion is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.MaximumVersion));
            }
            else if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    InvalidVersionFormatDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.MaximumVersion));
            }

            if (dataReaderTypeName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.DataReaderType));
            }

            if (dataReaderTaskTypeName is null)
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.DataReaderTaskType));
            }

            if (signatureAttributeTypes is null or { Length: 0 })
            {
                hasMisconfiguredInput = true;
                diagnostics ??= new();
                diagnostics.Add(
                    MissingRequiredPropertyDiagnostic.Create(
                        syntaxNode,
                        Constants.AdoNetInstrumentAttributeProperties.TargetMethodAttributes));
            }

            if (hasMisconfiguredInput)
            {
                continue;
            }

            foreach (var signatureAttributeName in signatureAttributeTypes!)
            {
                results ??= new();
                results.Add(
                    new AssemblyCallTargetDefinitionSource(
                        signatureAttributeName: signatureAttributeName,
                        integrationName: integrationName!,
                        assemblyName: assemblyName!,
                        targetTypeName: typeName!,
                        minimumVersion: minVersion,
                        maximumVersion: maxVersion,
                        isAdoNetIntegration: true,
                        instrumentationCategory: InstrumentationCategory.Tracing,
                        location: LocationInfo.CreateFrom(syntaxNode),
                        dataReaderTypeName,
                        dataReaderTaskTypeName));
            }
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        return new Result<EquatableArray<AssemblyCallTargetDefinitionSource>>(results is null ? default : new(results.ToArray()), errors);
    }

    private static Result<CallTargetDefinitionSource?> MergeAdoNetAttributes(
        AssemblyCallTargetDefinitionSource attribute, ImmutableArray<(string ClassName, AdoNetSignature Signature)> signatures)
    {
        foreach (var signature in signatures)
        {
            if (signature.ClassName == attribute.SignatureAttributeName)
            {
                // found it

                var returnTypeName = signature.Signature.ReturnType switch
                {
                    1 => attribute.DataReaderTypeName,
                    2 => attribute.DataReaderTaskTypeName,
                    _ => signature.Signature.TargetReturnType
                };

                var callTargetSource =
                    new CallTargetDefinitionSource(
                        integrationName: attribute.IntegrationName!,
                        assemblyName: attribute.AssemblyName!,
                        targetTypeName: attribute.TargetTypeName!,
                        targetMethodName: signature.Signature.TargetMethodName,
                        targetReturnType: returnTypeName!,
                        targetParameterTypes: signature.Signature.TargetParameterTypes.AsArray() ?? [],
                        minimumVersion: attribute.MinimumVersion,
                        maximumVersion: attribute.MaximumVersion,
                        instrumentationTypeName: signature.Signature.InstrumentationTypeName,
                        integrationKind: signature.Signature.CallTargetIntegrationKind,
                        isAdoNetIntegration: true,
                        instrumentationCategory: InstrumentationCategory.Tracing);

                return new Result<CallTargetDefinitionSource?>(callTargetSource, default);
            }
        }

        var diagnostic = UnknownAdoNetSignatureNameDiagnostic.Create(
            attribute.Location,
            attribute.SignatureAttributeName);

        return new Result<CallTargetDefinitionSource?>(null, new([diagnostic]));
    }

    private static void Execute(
        ImmutableArray<CallTargetDefinitionSource> definitions,
        ImmutableArray<CallTargetDefinitionSource> adoNetDefinitions,
        SourceProductionContext context)
    {
        if (definitions.IsDefaultOrEmpty && adoNetDefinitions.IsDefaultOrEmpty)
        {
            return;
        }

        var allDefinitions = definitions.IsDefaultOrEmpty
                                 ? adoNetDefinitions
                                 : (adoNetDefinitions.IsDefaultOrEmpty ? definitions : definitions.AddRange(adoNetDefinitions));

        string source = Sources.CreateCallTargetDefinitions(allDefinitions);
        context.AddSource("InstrumentationDefinitions.g.cs", SourceText.From(source, Encoding.UTF8));
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

        if (maybeParamValues.Value.IsDefaultOrEmpty)
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

        if (maybeParamValues.Value.IsDefaultOrEmpty)
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
