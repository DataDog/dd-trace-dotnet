// <copyright file="TracerSettingsSnapshotGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.TracerSettingsSnapshot;
using Datadog.Trace.SourceGenerators.TracerSettingsSnapshot.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <inheritdoc />
[Generator]
public class TracerSettingsSnapshotGenerator : IIncrementalGenerator
{
    private const string PublicApiAttributeFullName = "Datadog.Trace.SourceGenerators.PublicApiAttribute";
    private const string ConfigKeyAttributeFullName = "Datadog.Trace.SourceGenerators.ConfigKeyAttribute";
    private const string GenerateSnapshotFullName = "Datadog.Trace.SourceGenerators.GenerateSnapshotAttribute";
    private const string IgnoreForSnapshotFullName = "Datadog.Trace.SourceGenerators.IgnoreForSnapshotAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("GenerateSnapshotAttribute.g.cs", Sources.Attributes));

        var classesToGenerate =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        GenerateSnapshotFullName,
                        static (node, _) => node is ClassDeclarationSyntax,
                        static (context, ct) => GetSettableProperties(context, ct))
                   .WithTrackingName(TrackingNames.PostTransform)
                   .Where(static m => m is not null)!;

        context.ReportDiagnostics(
            classesToGenerate
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.Diagnostics));

        var validClasses = classesToGenerate
                          .Where(static m => m.Value.IsValid)
                          .Select(static (x, _) => x.Value.ClassToGenerate)
                          .WithTrackingName(TrackingNames.ValidValues);

        context.RegisterSourceOutput(
            validClasses,
            static (spc, classToGenerate) => Execute(in classToGenerate, spc));
    }

    private static void Execute(in SnapshotClass classToGenerate, SourceProductionContext context)
    {
        // Todo: cache this?
        StringBuilder sb = new StringBuilder();
        var result = Sources.GenerateSnapshotClass(sb, in classToGenerate);
        context.AddSource(classToGenerate.SnapshotClassName + ".g.cs", SourceText.From(result, Encoding.UTF8));
    }

    private static Result<(SnapshotClass ClassToGenerate, bool IsValid)> GetSettableProperties(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        var classSymbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (classSymbol is null)
        {
            // nothing to do if this type isn't available
            return new Result<(SnapshotClass, bool)>((default, false), default);
        }

        var classMembers = classSymbol.GetMembers();
        var members = new List<SettableProperty>(classMembers.Length);
        List<DiagnosticInfo>? diagnostics = null;

        foreach (var member in classMembers)
        {
            string? configurationKey = null;
            if (member is not IPropertySymbol property)
            {
                continue;
            }

            if (property.IsReadOnly)
            {
                // check if it's a "collection" type. This isn't perfect, but should catch most cases.
                bool isCollection = false;
                foreach (var i in property.Type.AllInterfaces)
                {
                    if (i.Name == "System.Collections.ICollection")
                    {
                        isCollection = true;
                    }
                }

                if (!isCollection)
                {
                    continue;
                }
            }

            // We don't want to touch APIs decorated with [PublicApi] attribute
            var ignore = false;
            foreach (var attribute in member.GetAttributes())
            {
                if ((attribute.AttributeClass?.Name == "PublicApiAttribute"
                  || attribute.AttributeClass?.Name == "PublicApi")
                 && attribute.AttributeClass.ToDisplayString() == PublicApiAttributeFullName)
                {
                    ignore = true;
                    break;
                }

                if ((attribute.AttributeClass?.Name == "ConfigKeyAttribute"
                  || attribute.AttributeClass?.Name == "ConfigKey")
                 && attribute.AttributeClass.ToDisplayString() == ConfigKeyAttributeFullName)
                {
                    if (attribute.ConstructorArguments[0].Value?.ToString() is { } key)
                    {
                        configurationKey = key;
                    }
                }

                if ((attribute.AttributeClass?.Name == "IgnoreForSnapshotAttribute"
                  || attribute.AttributeClass?.Name == "IgnoreForSnapshot")
                 && attribute.AttributeClass.ToDisplayString() == IgnoreForSnapshotFullName)
                {
                    ignore = true;
                    break;
                }
            }

            if (ignore)
            {
                continue;
            }

            if (string.IsNullOrEmpty(configurationKey))
            {
                diagnostics ??= new List<DiagnosticInfo>();
                foreach (var syntax in property.DeclaringSyntaxReferences)
                {
                    diagnostics.Add(MissingConfigKeyAttributeDiagnostic.CreateInfo(syntax.GetSyntax()));
                }
            }
            else
            {
                members.Add(new(property.Name, property.Type.ToDisplayString(), configurationKey!));
            }
        }

        string name = classSymbol.Name + "Snapshot";
        string nameSpace = classSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : classSymbol.ContainingNamespace.ToString();

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        var result = new SnapshotClass(
            ns: nameSpace,
            fullyQualifiedOriginalClassName: classSymbol.ToDisplayString(),
            snapshotClassName: name,
            new EquatableArray<SettableProperty>(members.ToArray()));

        return new Result<(SnapshotClass PropertyTag, bool IsValid)>((result, true), errors);
    }
}
