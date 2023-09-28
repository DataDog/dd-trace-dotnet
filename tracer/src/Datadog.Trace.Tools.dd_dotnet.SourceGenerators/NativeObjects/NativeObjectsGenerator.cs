// <copyright file="NativeObjectsGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.SourceGenerators.NativeObjects;
using Microsoft.CodeAnalysis;

/// <summary>
/// Source generator to generate native object wrappers for COM objects to use with NativeAOT.
/// </summary>
[Generator]
public class NativeObjectsGenerator : IIncrementalGenerator
{
    private const string Attribute = """
        using System;
        
        [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
        internal class NativeObjectAttribute : Attribute { }
        """;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName("NativeObjectAttribute", static (_, _) => true, Transform);
        context.RegisterSourceOutput(provider, static (ctx, result) => ctx.AddSource($"{result.Name}.g.cs", result.Source));
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("NativeObjectAttribute.g.cs", Attribute);
        });
    }

    private static (string Name, string Source) Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var sourceBuilder = new StringBuilder(Constants.FileHeader + @"
using System;
using System.Runtime.InteropServices;

namespace NativeObjects;

internal unsafe class {typeName} : {interfaceName}
{
    public static {typeName} Wrap(IntPtr obj) => new {typeName}(obj);

    private readonly IntPtr _implementation;

    public {typeName}(IntPtr implementation)
    {
        _implementation = implementation;
    }

    private nint* VTable => (nint*)*(nint*)_implementation;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_implementation != IntPtr.Zero)
        {
            Release();
        }
    }

    ~{typeName}()
    {
        Dispose();
    }

{invokerFunctions}

}
");

        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var interfaceName = symbol.ToString();
        var typeName = $"{symbol.Name}";
        int delegateCount = 0;
        var invokerFunctions = new StringBuilder();

        var interfaceList = symbol.AllInterfaces.ToList();
        interfaceList.Reverse();
        interfaceList.Add(symbol);

        foreach (var @interface in interfaceList)
        {
            if (!@interface.GetAttributes().Any(i => i.AttributeClass?.Name == "NativeObjectAttribute"))
            {
                continue;
            }

            bool iUnknown = @interface.Name == "IUnknown";

            foreach (var member in @interface.GetMembers())
            {
                if (member is not IMethodSymbol method)
                {
                    continue;
                }

                if (method.MethodKind == MethodKind.SharedConstructor)
                {
                    continue;
                }

                invokerFunctions.AppendIndented(1, $"public {method.ReturnType} {method.Name}(");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        invokerFunctions.Append(", ");
                    }

                    var refKind = method.Parameters[i].RefKind;

                    switch (refKind)
                    {
                        case RefKind.In:
                            invokerFunctions.Append("in ");
                            break;
                        case RefKind.Out:
                            invokerFunctions.Append("out ");
                            break;
                        case RefKind.Ref:
                            invokerFunctions.Append("ref ");
                            break;
                    }

                    invokerFunctions.Append($"{method.Parameters[i].Type} a{i}");
                }

                invokerFunctions.AppendLine(")");
                invokerFunctions.AppendLineIndented(1, "{");

                var freeMemory = new StringBuilder();
                var marshalStrings = new StringBuilder();

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    if (method.Parameters[i].Type.SpecialType == SpecialType.System_String && method.Parameters[i].RefKind != RefKind.Out)
                    {
                        marshalStrings.AppendLineIndented(2, $"var str{i} = Marshal.StringToBSTR(a{i});");
                        freeMemory.AppendLineIndented(2, $"Marshal.FreeBSTR(str{i});");
                    }
                }

                invokerFunctions.Append(marshalStrings);

                invokerFunctions.AppendIndented(2, "var func = (delegate* unmanaged[Stdcall]<IntPtr");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    invokerFunctions.Append(", ");

                    var refKind = method.Parameters[i].RefKind;

                    switch (refKind)
                    {
                        case RefKind.In:
                            invokerFunctions.Append("in ");
                            break;
                        case RefKind.Ref:
                            invokerFunctions.Append("ref ");
                            break;
                        case RefKind.Out:
                            invokerFunctions.Append("out ");
                            break;
                    }

                    // If type is string, use IntPtr instead
                    if (method.Parameters[i].Type.SpecialType == SpecialType.System_String)
                    {
                        invokerFunctions.Append("IntPtr");
                    }
                    else
                    {
                        invokerFunctions.Append(method.Parameters[i].Type);
                    }
                }

                if (iUnknown)
                {
                    invokerFunctions.Append($", {method.ReturnType}");
                }
                else
                {
                    if (!method.ReturnsVoid)
                    {
                        // Check if the NativeObject attribute is applied on target type
                        var isNativeObject = method.ReturnType.GetAttributes().Any(i => i.AttributeClass?.Name == "NativeObjectAttribute");

                        if (isNativeObject || method.ReturnType.SpecialType == SpecialType.System_String)
                        {
                            invokerFunctions.Append(", out IntPtr");
                        }
                        else
                        {
                            invokerFunctions.Append($", out {method.ReturnType}");
                        }
                    }

                    invokerFunctions.Append($", int");
                }

                invokerFunctions.AppendLine($">)*(VTable + {delegateCount});");

                invokerFunctions.AppendIndented(2, "var result = func(_implementation");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    invokerFunctions.Append($", ");

                    var refKind = method.Parameters[i].RefKind;

                    switch (refKind)
                    {
                        case RefKind.In:
                            invokerFunctions.Append("in ");
                            break;
                        case RefKind.Ref:
                            invokerFunctions.Append("ref ");
                            break;
                        case RefKind.Out:
                            invokerFunctions.Append("out ");
                            break;
                    }

                    if (method.Parameters[i].Type.SpecialType == SpecialType.System_String)
                    {
                        invokerFunctions.Append($"str{i}");
                    }
                    else
                    {
                        invokerFunctions.Append($"a{i}");
                    }
                }

                var assignations = new StringBuilder();

                if (!iUnknown && !method.ReturnsVoid)
                {
                    // Check if the NativeObject attribute is applied on target type
                    var isNativeObject = method.ReturnType.GetAttributes().Any(i => i.AttributeClass?.Name == "NativeObjectAttribute");

                    if (isNativeObject)
                    {
                        invokerFunctions.Append(", out var returnptr");
                        assignations.AppendLineIndented(2, $"var returnvalue = NativeObjects.{method.ReturnType.Name}.Wrap(returnptr);");
                    }
                    else if (method.ReturnType.SpecialType == SpecialType.System_String)
                    {
                        invokerFunctions.Append(", out var returnstr");
                        assignations.AppendLineIndented(2, "var returnvalue = Marshal.PtrToStringBSTR(returnstr);");
                        freeMemory.AppendLineIndented(2, "Marshal.FreeBSTR(returnstr);");
                    }
                    else
                    {
                        invokerFunctions.Append(", out var returnvalue");
                    }
                }

                invokerFunctions.AppendLine(");");

                invokerFunctions.Append(assignations);
                invokerFunctions.Append(freeMemory);

                if (!iUnknown)
                {
                    invokerFunctions.AppendLineIndented(2, "if (result != 0)");
                    invokerFunctions.AppendLineIndented(2, "{");
                    invokerFunctions.AppendLineIndented(3, "throw new System.ComponentModel.Win32Exception(result);");
                    invokerFunctions.AppendLineIndented(2, "}");
                }

                if (iUnknown)
                {
                    invokerFunctions.AppendLineIndented(2, "return result;");
                }
                else if (!method.ReturnsVoid)
                {
                    invokerFunctions.AppendLineIndented(2, "return returnvalue;");
                }

                invokerFunctions.AppendLineIndented(1, "}");

                delegateCount++;
            }
        }

        sourceBuilder.Replace("{typeName}", typeName);
        sourceBuilder.Replace("{interfaceName}", interfaceName);
        sourceBuilder.Replace("{invokerFunctions}", invokerFunctions.ToString());

        return ($"{symbol.ContainingNamespace?.Name ?? "_"}.{symbol.Name}", sourceBuilder.ToString());
    }
}
