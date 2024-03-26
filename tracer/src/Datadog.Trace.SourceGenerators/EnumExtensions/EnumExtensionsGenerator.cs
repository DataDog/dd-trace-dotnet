// <copyright file="EnumExtensionsGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.EnumExtensions;
using Datadog.Trace.SourceGenerators.EnumExtensions.Diagnostics;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <inheritdoc />
[Generator]
public class EnumExtensionsGenerator : IIncrementalGenerator
{
    private const string DescriptionAttribute = "System.ComponentModel.DescriptionAttribute";
    private const string EnumExtensionsAttribute = "Datadog.Trace.SourceGenerators.EnumExtensionsAttribute";
    private const string FlagsAttribute = "System.FlagsAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "EnumExtensionsAttribute.g.cs", SourceText.From(Sources.Attributes, Encoding.UTF8)));

        IncrementalValuesProvider<Result<(EnumToGenerate Enum, bool IsValid)>> enumsToGenerate =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                        EnumExtensionsAttribute,
                        predicate: (node, _) => node is EnumDeclarationSyntax,
                        transform: GetTypeToGenerate)
                   .WithTrackingName(TrackingNames.PostTransform)
                   .Where(static m => m is not null);

        context.ReportDiagnostics(
            enumsToGenerate
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.Diagnostics));

        var validEnumsToGenerate = enumsToGenerate
                                  .Where(static m => m.Value.IsValid)
                                  .Select(static (x, _) => x.Value.Enum)
                                  .WithTrackingName(TrackingNames.ValidValues);

        context.RegisterSourceOutput(
            validEnumsToGenerate,
            static (spc, enumToGenerate) => Execute(in enumToGenerate, spc));
    }

    private static void Execute(in EnumToGenerate enumToGenerate, SourceProductionContext context)
    {
        StringBuilder sb = new StringBuilder();
        var result = Sources.GenerateExtensionClass(sb, in enumToGenerate);
        context.AddSource(enumToGenerate.ExtensionsName + "_EnumExtensions.g.cs", SourceText.From(result, Encoding.UTF8));
    }

    private static Result<(EnumToGenerate Enum, bool IsValid)> GetTypeToGenerate(
        GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        INamedTypeSymbol? enumSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (enumSymbol is null)
        {
            // nothing to do if this type isn't available
            return new Result<(EnumToGenerate, bool)>((default, false), default);
        }

        ct.ThrowIfCancellationRequested();

        string name = enumSymbol.Name + "Extensions";
        string nameSpace = enumSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : enumSymbol.ContainingNamespace.ToString();
        var hasFlags = false;

        foreach (AttributeData attributeData in enumSymbol.GetAttributes())
        {
            if ((attributeData.AttributeClass?.Name == "FlagsAttribute" ||
                 attributeData.AttributeClass?.Name == "Flags") &&
                attributeData.AttributeClass.ToDisplayString() == FlagsAttribute)
            {
                hasFlags = true;
                break;
            }
        }

        string fullyQualifiedName = enumSymbol.ToString();

        var enumMembers = enumSymbol.GetMembers();
        var members = new List<(string, string?)>(enumMembers.Length);
        HashSet<string>? descriptions = null;
        List<DiagnosticInfo>? diagnostics = null;

        foreach (var member in enumMembers)
        {
            if (member is not IFieldSymbol field || field.ConstantValue is null)
            {
                continue;
            }

            string? description = null;
            foreach (var attribute in member.GetAttributes())
            {
                if ((attribute.AttributeClass?.Name == "DescriptionAttribute"
                     || attribute.AttributeClass?.Name == "Description")
                    && attribute.AttributeClass.ToDisplayString() == DescriptionAttribute
                    && attribute.ConstructorArguments.Length == 1)
                {
                    if (attribute.ConstructorArguments[0].Value?.ToString() is { } dn)
                    {
                        description = dn;
                        descriptions ??= new HashSet<string>();
                        if (!descriptions.Add(description))
                        {
                            diagnostics ??= new List<DiagnosticInfo>();
                            diagnostics.Add(DuplicateDescriptionDiagnostic.CreateInfo(attribute.ApplicationSyntaxReference?.GetSyntax()));
                        }

                        break;
                    }
                }
            }

            members.Add((member.Name, description));
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        var enumToGenerate = new EnumToGenerate(
            extensionsName: name,
            fullyQualifiedName: fullyQualifiedName,
            ns: nameSpace,
            hasFlags: hasFlags,
            names: members,
            hasDescriptions: descriptions is not null);

        return new Result<(EnumToGenerate, bool)>((enumToGenerate, true), errors);
    }
}
