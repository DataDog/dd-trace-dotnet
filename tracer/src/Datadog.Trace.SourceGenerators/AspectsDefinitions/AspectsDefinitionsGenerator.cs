// <copyright file="AspectsDefinitionsGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <inheritdoc />
[Generator]
public class AspectsDefinitionsGenerator : IIncrementalGenerator
{
    private const string NullLiteral = "null";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // RegisterSource(context, "AspectAttribute");
        // RegisterSource(context, "AspectClassAttribute");
        // RegisterSource(context, "AspectCtorReplaceAttribute");
        // RegisterSource(context, "AspectMethodInsertAfterAttribute");
        // RegisterSource(context, "AspectMethodInsertBeforeAttribute");
        // RegisterSource(context, "AspectMethodReplaceAttribute");

        IncrementalValuesProvider<Result<(ClassAspects Aspects, bool IsValid)>> aspectsClassesToGenerate =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                        "Datadog.Trace.Iast.Dataflow.AspectClassAttribute",
                        predicate: (node, _) => node is ClassDeclarationSyntax,
                        transform: GetAspectsToGenerate)
                   .WithTrackingName(TrackingNames.PostTransform)

                   .Where(static m => m is not null);

        context.ReportDiagnostics(
            aspectsClassesToGenerate
               .Where(static m => m.Errors.Count > 0)
               .SelectMany(static (x, _) => x.Errors)
               .WithTrackingName(TrackingNames.Diagnostics));

        IncrementalValuesProvider<ClassAspects> validClassesToGenerate =
            aspectsClassesToGenerate
               .Where(static m => m.Value.IsValid)
               .Select(static (x, _) => x.Value.Aspects)
               .WithTrackingName(TrackingNames.ValidValues);

        IncrementalValueProvider<ImmutableArray<ClassAspects>> allClassesToGenerate =
            validClassesToGenerate
               .Collect()
               .WithTrackingName(TrackingNames.Collected);

        context.RegisterSourceOutput(
            allClassesToGenerate,
            static (spc, classToGenerate) => Execute(in classToGenerate, spc));
    }

    private static void Execute(in ImmutableArray<ClassAspects> aspectClasses, SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.Append(Datadog.Trace.SourceGenerators.Constants.FileHeader);
        sb.AppendLine("""
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] GetAspects() => new string[] {
""");

        foreach (var aspectClass in aspectClasses.OrderBy(p => p.AspectClass, StringComparer.Ordinal))
        {
            sb.AppendLine(FormatLine(aspectClass.AspectClass));
            foreach (var aspect in aspectClass.Aspects)
            {
                sb.AppendLine(FormatLine(aspect));
            }
        }

        sb.AppendLine("""
        };

""");

        sb.AppendLine("""
        public static string[] GetRaspAspects() => new string[] {
""");

        foreach (var aspectClass in aspectClasses.OrderBy(p => p.AspectClass, StringComparer.Ordinal))
        {
            if (aspectClass.AspectClass.Contains(",RaspIastSink,"))
            {
                sb.AppendLine(FormatLine(aspectClass.AspectClass));
                foreach (var aspect in aspectClass.Aspects)
                {
                    sb.AppendLine(FormatLine(aspect));
                }
            }
        }

        sb.AppendLine("""
        };
    }
}
""");

        context.AddSource("AspectsDefinitions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));

        string FormatLine(string line)
        {
            return $"\"{line.Replace("\"", "\\\"").Replace("RaspIastSink", "Sink")}\",";
        }
    }

    private static Result<(ClassAspects Aspects, bool IsValid)> GetAspectsToGenerate(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        INamedTypeSymbol? classSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (classSymbol is null)
        {
            // nothing to do if this type isn't available
            return new Result<(ClassAspects, bool)>((default, false), default);
        }

        ct.ThrowIfCancellationRequested();
        List<DiagnosticInfo>? diagnostics = null;

        string aspectClass = string.Empty;
        foreach (AttributeData attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is null) { continue; }
            if (attribute.AttributeClass.Name == "AspectClassAttribute")
            {
                var line = GetAspectLine(attribute);
                aspectClass = line + " " + classSymbol.ToString();

                break;
            }
        }

        var aspects = new List<string>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            foreach (var attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass is null) { continue; }
                if (attribute.AttributeClass.Name.StartsWith("Aspect"))
                {
                    var line = GetAspectLine(attribute);
                    aspects.Add("  " + line + " " + GetFullName(method));
                }
            }
        }

        var errors = diagnostics is { Count: > 0 }
                         ? new EquatableArray<DiagnosticInfo>(diagnostics.ToArray())
                         : default;

        return new Result<(ClassAspects, bool)>((new ClassAspects(aspectClass, aspects), true), errors);
    }

    private static string GetAspectLine(AttributeData data)
    {
        if (data is null || data.AttributeClass is null) { return string.Empty; }

        var arguments = data.ConstructorArguments.Select(GetArgument).ToList();
        var name = data.AttributeClass.Name;
        var version = string.Empty;

        if (name.EndsWith("FromVersionAttribute"))
        {
            // Aspect with version limitation
            name = name.Replace("FromVersionAttribute", "Attribute");
            version = ";V" + arguments[0].Trim('"');
            arguments.RemoveAt(0);
        }

        return name switch
        {
            // Coments are to have the original attributes overloads present
            // AspectClassAttribute(string defaultAssembly, AspectFilter[] filters, AspectType defaultAspectType = AspectType.Propagation, VulnerabilityType[] defaultVulnerabilityTypes)
            "AspectClassAttribute" => arguments.Count switch
            {
                // AspectClassAttribute(string defaultAssembly)
                1 => $"[AspectClass({arguments[0]},[None],Propagation,[]){version}]",
                // AspectClassAttribute(string defaultAssembly, AspectType defaultAspectType, params VulnerabilityType[] defaultVulnerabilityTypes)
                3 => $"[AspectClass({arguments[0]},[None],{arguments[1]},{Check(arguments[2])}){version}]",
                // AspectClassAttribute(string defaultAssembly, AspectFilter[] filters, AspectType defaultAspectType = AspectType.Propagation, params VulnerabilityType[] defaultVulnerabilityTypes)
                4 => $"[AspectClass({arguments[0]},{arguments[1]},{arguments[2]},{Check(arguments[3])}){version}]",
                _ => throw new ArgumentException($"Could not find AspectClassAttribute overload with {arguments.Count} parameters")
            },
            // AspectAttribute(string targetMethod, string targetType, int[] paramShift, bool[] boxParam, AspectFilter[] filters, AspectType aspectType = AspectType.Propagation, VulnerabilityType[] vulnerabilityTypes)
            "AspectCtorReplaceAttribute" => arguments.Count switch
            {
                // AspectCtorReplaceAttribute(string targetMethod)
                1 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],[None],Default,[]){version}]",
                // AspectCtorReplaceAttribute(string targetMethod, params AspectFilter[] filters)
                2 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],{Check(arguments[1])},Default,[]){version}]",
                // AspectCtorReplaceAttribute(string targetMethod, AspectType aspectType = AspectType.Default, params VulnerabilityType[] vulnerabilityTypes)
                3 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],[None],{arguments[1]},{Check(arguments[2])}){version}]",
                // AspectCtorReplaceAttribute(string targetMethod, AspectFilter[] filters, AspectType aspectType = AspectType.Default, params VulnerabilityType[] vulnerabilityTypes)
                4 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],[{arguments[1]}],{arguments[2]},{Check(arguments[3])}){version}]",
                _ => throw new ArgumentException($"Could not find AspectCtorReplaceAttribute overload with {arguments.Count} parameters")
            },
            "AspectMethodReplaceAttribute" => arguments.Count switch
            {
                // AspectMethodReplaceAttribute(string targetMethod)
                1 => $"[AspectMethodReplace({arguments[0]},\"\",[0],[False],[None],Default,[]){version}]",
                // AspectMethodReplaceAttribute(string targetMethod, params AspectFilter[] filters)
                2 => $"[AspectMethodReplace({arguments[0]},\"\",[0],[False],{Check(arguments[1], "[None]")},Default,[]){version}]",
                // AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
                3 => arguments[1] switch
                {
                    { } when arguments[1].StartsWith("[") => $"[AspectMethodReplace({arguments[0]},\"\",{arguments[1]},{arguments[2]},[None],Default,[]){version}]",
                    // AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
                    _ => $"[AspectMethodReplace({arguments[0]},{arguments[1]},[0],[False],{Check(arguments[2], "[None]")},Default,[]){version}]",
                },
                _ => throw new ArgumentException($"Could not find AspectMethodReplaceAttribute overload with {arguments.Count} parameters")
            },
            "AspectMethodInsertBeforeAttribute" => arguments.Count switch
            {
                // AspectMethodInsertBeforeAttribute(string targetMethod, params int[] paramShift)
                2 => $"[AspectMethodInsertBefore({arguments[0]},\"\",{MakeSameSize(Check(arguments[1]))},[None],Default,[]){version}]",
                // AspectMethodInsertBeforeAttribute(string targetMethod, int[] paramShift, bool[] boxParam)
                3 => $"[AspectMethodInsertBefore({arguments[0]},\"\",[{arguments[1]}],[{arguments[2]}],[None],Default,[]){version}]",
                _ => throw new ArgumentException($"Could not find AspectMethodInsertBeforeAttribute overload with {arguments.Count} parameters")
            },
            "AspectMethodInsertAfterAttribute" => arguments.Count switch
            {
                // AspectMethodInsertAfterAttribute(string targetMethod)
                1 => $"[AspectMethodInsertAfter({arguments[0]},\"\",[0],[False],[None],Default,[]){version}]",
                // AspectMethodInsertAfterAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
                3 => $"[AspectMethodInsertAfter({arguments[0]},\"\",[0],[False],[None],{arguments[1]},{Check(arguments[2])}){version}]",
                _ => throw new ArgumentException($"Could not find AspectMethodInsertAfterAttribute overload with {arguments.Count} parameters")
            },
            _ => throw new Exception()
        };

        string Check(string val, string ifEmpty = "[]")
        {
            return (string.IsNullOrEmpty(val) || val == NullLiteral || val == "[]") ? ifEmpty : val;
        }

        string MakeSameSize(string val, string ifEmpty = "[0]", string defaultValue = "False")
        {
            val = Check(val, ifEmpty);
            int count = val.Count(c => c == ',');
            string values = string.Empty;
            for (int x = 0; x < count + 1; x++)
            {
                values += defaultValue;
                if (x < count) { values += ","; }
            }

            return $"{val},[{values}]";
        }
    }

    private static DiagnosticInfo CreateInfo(SyntaxNode? currentNode)
    => new(
        new DiagnosticDescriptor(
            "AG1",
            "AspectsGenerator Error",
            "Error message",
            category: Datadog.Trace.SourceGenerators.Constants.Usage,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true),
        currentNode?.GetLocation());

    private static Type? Resolve(TypedConstant? constant)
    {
        if (constant == null) { return null; }
        var typeName = GetFullName(constant?.Type);
        var type = Type.GetType(typeName);
        return type;
    }

    private static string GetFullName(ITypeSymbol? type, bool extractOnlyBaseType = true)
    {
        if (type == null) { return string.Empty; }
        if (type is IArrayTypeSymbol arrayType)
        {
            var elementType = GetFullName(arrayType.ElementType, extractOnlyBaseType);
            if (extractOnlyBaseType) { return elementType; }
            return elementType + "[]";
        }

        var ns = string.Empty;
        if (type.ContainingSymbol is INamespaceSymbol nameSpace)
        {
            ns = nameSpace.ToString();
        }

        var name = type.Name.ToString();
        if (type is INamedTypeSymbol namedType)
        {
            if (ns.Length > 0) { name = ns + "." + name; }
            if (namedType.TypeArguments.Length > 0)
            {
                name = $"{name}`{namedType.TypeArguments.Length}<{string.Join(",", namedType.TypeArguments.Select(a => GetFullName(a, false)))}>";
            }
        }
        else if (type is ITypeParameterSymbol typeParameter)
        {
            name = $"!!{typeParameter.Ordinal}";
        }

        return name;
    }

    private static string GetFullName(IMethodSymbol method)
    {
        string arguments = string.Join(",", method.Parameters.Select(a => GetFullName(a.Type, false)));
        return $"{method.Name}({arguments})";
    }

    private static string GetArgument(TypedConstant customAttributeArgument)
    {
        if (customAttributeArgument.Type is null) { return NullLiteral; }
        else if (customAttributeArgument.Kind == TypedConstantKind.Primitive)
        {
            var value = customAttributeArgument.Value?.ToString() ?? NullLiteral;
            if (customAttributeArgument.Type!.Name == "String") { return $"\"{value}\""; }
            return value;
        }
        else if (customAttributeArgument.Kind == TypedConstantKind.Enum)
        {
            if (customAttributeArgument.Type!.Name == "AspectFilter") { return ((AspectFilter)customAttributeArgument.Value!).ToString(); }
            else if (customAttributeArgument.Type!.Name == "AspectType") { return ((AspectType)customAttributeArgument.Value!).ToString(); }
            else if (customAttributeArgument.Type!.Name == "VulnerabilityType") { return ((VulnerabilityType)customAttributeArgument.Value!).ToString(); }
            var type = Resolve(customAttributeArgument);
            return Enum.ToObject(type, customAttributeArgument.Value).ToString();
        }
        else if (customAttributeArgument.Kind == TypedConstantKind.Array)
        {
            var elementType = Resolve(customAttributeArgument);
            var values = customAttributeArgument.Values;
            return $"[{string.Join(",", values.Select(GetArgument))}]";
        }

        return string.Empty;
    }

    internal record struct ClassAspects
    {
        public readonly string AspectClass;
        public readonly EquatableArray<string> Aspects;

        public ClassAspects(string aspectClass, IEnumerable<string> aspects)
        {
            AspectClass = aspectClass;
            Aspects = new EquatableArray<string>(aspects.ToArray());
        }
    }
}
