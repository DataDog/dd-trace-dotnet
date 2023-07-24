// <copyright file="AspectsDefinitionsGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.Iast.Dataflow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.SourceGenerators.AspectsDefinitions;

/// <inheritdoc />
[Generator]
public class AspectsDefinitionsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG__
        if (!Debugger.IsAttached)
        {
            Debugger.Launch();
        }
#endif

        // Register the attribute source

        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations =
            context.SyntaxProvider.CreateSyntaxProvider(
                        static (node, token) => IsAttributedClass(node, token),
                        static (syntaxContext, token) => GetPotentialClassesForGeneration(syntaxContext, token))
                   .Where(static m => m is not null)!;

        IncrementalValuesProvider<AttributeData> assemblyAttributes =
           context.CompilationProvider.SelectMany(static (compilation, _) => compilation.Assembly.GetAttributes());

        IncrementalValueProvider<(Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right)> compilationAndClasses =
           context.CompilationProvider
                  .Combine(classDeclarations.Collect());

        IncrementalValueProvider<IReadOnlyList<AspectClassDefinition>> detailsToRender =
           compilationAndClasses.Select(static (x, ct) => GetDefinitionsToWrite(x.Left, x.Right, ct));
        context.RegisterSourceOutput(detailsToRender, static (spc, source) => Execute(source, spc));
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
                if (attributeSyntax.Name.ToString().Contains("AspectClass") || attributeSyntax.Name.ToString() == Constants.AspectClassAttributeName)
                {
                    return classDeclarationSyntax;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<AspectClassDefinition> GetDefinitionsToWrite(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, CancellationToken ct)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return Array.Empty<AspectClassDefinition>();
        }

        return GetAspectSources(compilation, classes.Distinct(), ct);
    }

    private static void Execute(IReadOnlyList<AspectClassDefinition> definitions, SourceProductionContext context)
    {
        string source = GetSource(definitions);
        context.AddSource("AspectsDefinitions.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static IReadOnlyList<AspectClassDefinition> GetAspectSources(Compilation compilation, IEnumerable<ClassDeclarationSyntax> classes, CancellationToken cancellationToken)
    {
        INamedTypeSymbol? aspectClassAttribute = compilation.GetTypeByMetadataName(Constants.AspectClassAttributeFullName);
        if (aspectClassAttribute is null)
        {
            // nothing to do if this type isn't available
            return Array.Empty<AspectClassDefinition>();
        }

        var results = new List<AspectClassDefinition>();

        List<(string, INamedTypeSymbol)> classSymbols = new List<(string, INamedTypeSymbol)>();

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
                if (!aspectClassAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var instance = CreateInstance(attributeData);
                if (instance != null)
                {
                    string className = GetFullName(classSymbol);
                    classSymbols.Add((instance.ToString() + " " + className, classSymbol));
                }
            }
        }

        foreach (var classSymbol in classSymbols)
        {
            List<string> aspects = new List<string>();
            foreach (var member in classSymbol.Item2.GetMembers())
            {
                var memberAttributes = member.GetAttributes();
                foreach (var memberAttribute in memberAttributes)
                {
                    var attribute = CreateInstance(memberAttribute);
                    if (attribute != null)
                    {
                        string functionName = GetFullName(member);
                        aspects.Add("  " + attribute.ToString() + " " + functionName);
                    }
                }
            }

            aspects.Sort(StringComparer.InvariantCulture);
            results.Add(new AspectClassDefinition { AspectClass = classSymbol.Item1, Aspects = aspects });
        }

        return results;
    }

    private static object? CreateInstance(AttributeData? attribute)
    {
        if (attribute == null) { return null; }
        try
        {
            var arguments = attribute.ConstructorArguments.Select(a => GetArgument(a)).ToArray();
            var type = Resolve(attribute);
            if (type == null) { return null; }
            var res = Activator.CreateInstance(type, arguments);
            return res;
        }
        catch (Exception err)
        {
            Console.WriteLine("Error: " + err.ToString());
            throw;
        }
    }

    private static object? GetArgument(TypedConstant customAttributeArgument)
    {
        if (customAttributeArgument.Kind == TypedConstantKind.Primitive)
        {
            return customAttributeArgument.Value;
        }
        else if (customAttributeArgument.Kind == TypedConstantKind.Enum)
        {
            var type = Resolve(customAttributeArgument);
            return Enum.ToObject(type, customAttributeArgument.Value);
        }
        else if (customAttributeArgument.Kind == TypedConstantKind.Array)
        {
            var elementType = Resolve(customAttributeArgument);
            var values = customAttributeArgument.Values;
            return values.Select(a => a.Value).ToArray(elementType);
        }

        return null;
    }

    private static Type Resolve(AttributeData? attribute)
    {
        Type[] aspects = new Type[] { typeof(AspectClassAttribute), typeof(AspectMethodReplaceAttribute), typeof(AspectMethodInsertBeforeAttribute), typeof(AspectMethodInsertAfterAttribute), typeof(AspectCtorReplaceAttribute) };
        var name = attribute?.AttributeClass?.Name ?? string.Empty;
        var aspect = aspects.First(a => a.Name == name);
        return aspect;
    }

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

        var ns = type.ContainingSymbol?.ToString() ?? string.Empty;
        var name = type.Name.ToString();
        if (ns.Length > 0) { return ns + "." + name; }
        return name;
    }

    private static string GetFullName(ISymbol? member)
    {
        if (member != null && member is IMethodSymbol methodSymbol)
        {
            string arguments = string.Join(",", methodSymbol.Parameters.Select(a => GetFullName(a.Type, false)));
            return $"{member.Name}({arguments})";
        }

        return string.Empty;
    }

    private static string GetSource(IReadOnlyList<AspectClassDefinition> definitions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
// <auto-generated/>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    internal static partial class AspectDefinitions
    {
        public static string[] Aspects = new string[] {
""");

        foreach (var definition in definitions.OrderBy(d => d.AspectClass, StringComparer.InvariantCulture))
        {
            sb.Append("\"");
            sb.Append(definition.AspectClass.Replace("\"", "\\\""));
            sb.AppendLine("\",");
            foreach (var aspect in definition.Aspects)
            {
                sb.Append("\"");
                sb.Append(aspect.Replace("\"", "\\\""));
                sb.AppendLine("\",");
            }
        }

        sb.AppendLine("""
        };
    }
}
""");
        return sb.ToString();
    }

    private struct AspectClassDefinition
    {
        public string AspectClass;
        public List<string> Aspects;
    }
}
