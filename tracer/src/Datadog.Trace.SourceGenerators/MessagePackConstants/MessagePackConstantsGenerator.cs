// <copyright file="MessagePackConstantsGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Helpers;
using Datadog.Trace.SourceGenerators.MessagePackConstants;
using Datadog.Trace.SourceGenerators.MessagePackConstants.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <inheritdoc />
[Generator]
public class MessagePackConstantsGenerator : IIncrementalGenerator
{
    private const string MessagePackFieldAttributeFullName = "Datadog.Trace.SourceGenerators.MessagePackFieldAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("MessagePackFieldAttribute.g.cs", Sources.Attribute));

        // Find all const string fields with [MessagePackField] attribute
        var messagePackFields =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                        MessagePackFieldAttributeFullName,
                        static (node, _) => true, // Accept all nodes for now
                        static (context, ct) => GetFieldToSerialize(context, ct))
                   .Where(static m => m is not null)!
                   .WithTrackingName(TrackingNames.FieldResults);

        // Report diagnostics
        context.ReportDiagnostics(
            messagePackFields
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.Diagnostics));

        // Collect all valid fields
        var allFields = messagePackFields
                       .Where(static m => m.Value.IsValid)
                       .Select(static (x, _) => x.Value.Field)
                       .Collect()
                       .WithTrackingName(TrackingNames.AllFields);

        // Generate output
        context.RegisterSourceOutput(
            allFields,
            static (spc, fields) => Execute(fields, spc));
    }

    private static void Execute(ImmutableArray<FieldToSerialize> fields, SourceProductionContext context)
    {
        if (fields.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        // Detect duplicate field names
        var fieldGroups = fields.GroupBy(f => f.FieldName).ToList();
        var duplicateGroups = fieldGroups.Where(g => g.Count() > 1).ToList();

        if (duplicateGroups.Any())
        {
            foreach (var group in duplicateGroups)
            {
                var fieldName = group.Key;

                // Report diagnostic for each duplicate occurrence (skip first)
                foreach (var field in group.Skip(1))
                {
                    var diagnostic = DuplicateFieldNameDiagnostic.Create(fieldName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Only keep the first occurrence of each field name
        var uniqueFields = fieldGroups.Select(g => g.First()).ToImmutableArray();

        var source = Sources.CreateMessagePackConstants(uniqueFields);
        context.AddSource("MessagePackConstants.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static Result<(FieldToSerialize Field, bool IsValid)> GetFieldToSerialize(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        // ForAttributeWithMetadataName already provides the symbol via TargetSymbol
        var fieldSymbol = ctx.TargetSymbol as IFieldSymbol;
        List<DiagnosticInfo>? diagnostics = null;
        var hasMisconfiguredInput = false;

        // Verify it's a const string field
        if (fieldSymbol == null || !fieldSymbol.IsConst || fieldSymbol.Type.SpecialType != SpecialType.System_String)
        {
            diagnostics ??= new List<DiagnosticInfo>();
            diagnostics.Add(InvalidFieldDiagnostic.CreateInfo(ctx.TargetNode));
            hasMisconfiguredInput = true;
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        if (hasMisconfiguredInput || fieldSymbol == null)
        {
            return new Result<(FieldToSerialize Field, bool IsValid)>((default, false), errors);
        }

        var constantValue = fieldSymbol.ConstantValue as string;
        if (string.IsNullOrEmpty(constantValue))
        {
            diagnostics ??= new List<DiagnosticInfo>();
            diagnostics.Add(InvalidFieldDiagnostic.CreateInfo(ctx.TargetNode));
            return new Result<(FieldToSerialize Field, bool IsValid)>(
                (default, false),
                new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
        }

        var field = new FieldToSerialize(
            fieldName: fieldSymbol.Name,
            stringValue: constantValue!);

        return new Result<(FieldToSerialize Field, bool IsValid)>((field, true), errors);
    }

    internal static class TrackingNames
    {
        public const string FieldResults = nameof(FieldResults);
        public const string Diagnostics = nameof(Diagnostics);
        public const string AllFields = nameof(AllFields);
    }
}
