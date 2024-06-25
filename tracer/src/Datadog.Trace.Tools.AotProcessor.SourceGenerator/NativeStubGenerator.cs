// <copyright file="NativeStubGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Microsoft.CodeAnalysis;

namespace NativeStubSourceGenerator;
#pragma warning disable SA1118 // Parameter should not span multiple lines

/// <summary>
/// Native object generator.
/// </summary>
[Generator]
public class NativeStubGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the generator.
    /// </summary>
    /// <param name="context"> Source generator context </param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName("NativeObjectAttribute", static (_, _) => true, Transform);
        context.RegisterSourceOutput(provider, static (ctx, result) => ctx.AddSource($"{result.Name}.g.cs", result.Source));
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("NativeObjectAttribute.g.cs", @"using System;

[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
internal class NativeObjectAttribute : Attribute { }");
        });
    }

    private static (string Name, string Source) Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var sourceBuilder = new StringBuilder(@"
using System;
using System.Runtime.InteropServices;

namespace NativeObjects
{
    {visibility} unsafe class {typeName} : IDisposable
    {
        private {typeName}({interfaceName} implementation)
        {
            const int delegateCount = {delegateCount};

            var vtable = (IntPtr*)NativeMemory.Alloc((nuint)delegateCount, (nuint)IntPtr.Size);

{functionPointers}

            var obj = (IntPtr*)NativeMemory.Alloc((nuint)2, (nuint)IntPtr.Size);
            *obj = (IntPtr)vtable;

            var handle = GCHandle.Alloc(implementation);
            *(obj + 1) = GCHandle.ToIntPtr(handle);

            Object = (IntPtr)obj;
        }

        public IntPtr Object { get; private set; }

        public static {typeName} Wrap({interfaceName} implementation) => new(implementation);

        public static implicit operator IntPtr({typeName} stub) => stub.Object;

        ~{typeName}()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Object != IntPtr.Zero)
            {
                var target = (void**)Object;
                NativeMemory.Free(*target);
                NativeMemory.Free(target);
                Object = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        private static class Exports
        {
{exports}
        }
    }

    internal unsafe struct {invokerName}
    {
        private readonly IntPtr _implementation;

        public {invokerName}(IntPtr implementation)
        {
            _implementation = implementation;
        }

        private nint* VTable => (nint*)*(nint*)_implementation;

    {invokerFunctions}
 
    }
}
");

        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var interfaceName = symbol.ToString();
        var typeName = $"{symbol.Name}";
        var invokerName = $"{symbol.Name}Invoker";
        int delegateCount = 0;
        var exports = new StringBuilder();
        var functionPointers = new StringBuilder();
        var invokerFunctions = new StringBuilder();
        var visibility = symbol.DeclaredAccessibility.ToString().ToLower();

        var interfaceList = symbol.AllInterfaces.ToList();
        interfaceList.Reverse();
        interfaceList.Add(symbol);

        foreach (var @interface in interfaceList)
        {
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

                var parameterList = new StringBuilder();

                parameterList.Append("IntPtr* self");

                foreach (var parameter in method.Parameters)
                {
                    var isPointer = parameter.RefKind == RefKind.None ? string.Empty : "*";

                    parameterList.Append($", {parameter.Type}{isPointer} __arg{parameter.Ordinal}");
                }

                exports.AppendLine($"            [UnmanagedCallersOnly]");
                exports.AppendLine($"            public static {method.ReturnType} {method.Name}({parameterList})");
                exports.AppendLine($"            {{");
                exports.AppendLine($"                var handle = GCHandle.FromIntPtr(*(self + 1));");
                exports.AppendLine($"                var obj = ({interfaceName})handle.Target;");
                exports.Append($"                ");

                if (!method.ReturnsVoid)
                {
                    exports.Append("var result = ");
                }

                exports.Append($"obj.{method.Name}(");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        exports.Append(", ");
                    }

                    if (method.Parameters[i].RefKind == RefKind.In)
                    {
                        exports.Append($"*__arg{i}");
                    }
                    else if (method.Parameters[i].RefKind is RefKind.Out)
                    {
                        exports.Append($"out var __local{i}");
                    }
                    else
                    {
                        exports.Append($"__arg{i}");
                    }
                }

                exports.AppendLine(");");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    if (method.Parameters[i].RefKind is RefKind.Out)
                    {
                        exports.AppendLine($"if (__arg{i} != null)                *__arg{i} = __local{i};");
                    }
                }

                if (!method.ReturnsVoid)
                {
                    exports.AppendLine($"                return result;");
                }

                exports.AppendLine($"            }}");

                exports.AppendLine();
                exports.AppendLine();

                var sourceArgsList = new StringBuilder();
                sourceArgsList.Append("IntPtr _");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    sourceArgsList.Append($", ");

                    var refKind = method.Parameters[i].RefKind;

                    switch (refKind)
                    {
                        case RefKind.In:
                            sourceArgsList.Append("in ");
                            break;
                        case RefKind.Out:
                            sourceArgsList.Append("out ");
                            break;
                        case RefKind.Ref:
                            sourceArgsList.Append("ref ");
                            break;
                    }

                    sourceArgsList.Append(method.Parameters[i].Type);
                    sourceArgsList.Append($" a{i}");
                }

                functionPointers.Append($"            *(vtable + {delegateCount}) = (IntPtr)(delegate* unmanaged<IntPtr*");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    functionPointers.Append($", {method.Parameters[i].Type}");

                    if (method.Parameters[i].RefKind != RefKind.None)
                    {
                        functionPointers.Append("*");
                    }
                }

                if (method.ReturnsVoid)
                {
                    functionPointers.Append(", void");
                }
                else
                {
                    functionPointers.Append($", {method.ReturnType}");
                }

                functionPointers.AppendLine($">)&Exports.{method.Name};");

                invokerFunctions.Append($"            public {method.ReturnType} {method.Name}(");

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
                invokerFunctions.AppendLine("            {");

                invokerFunctions.Append("                var func = (delegate* unmanaged[Stdcall]<IntPtr");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    invokerFunctions.Append(", ");

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

                    invokerFunctions.Append(method.Parameters[i].Type);
                }

                invokerFunctions.AppendLine($", {method.ReturnType}>)*(VTable + {delegateCount});");

                invokerFunctions.Append("                ");

                if (method.ReturnType.SpecialType != SpecialType.System_Void)
                {
                    invokerFunctions.Append("return ");
                }

                invokerFunctions.Append("func(_implementation");

                for (int i = 0; i < method.Parameters.Length; i++)
                {
                    invokerFunctions.Append($", ");

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

                    invokerFunctions.Append($"a{i}");
                }

                invokerFunctions.AppendLine(");");

                invokerFunctions.AppendLine("            }");

                delegateCount++;
            }
        }

        sourceBuilder.Replace("{typeName}", typeName);
        sourceBuilder.Replace("{visibility}", visibility);
        sourceBuilder.Replace("{exports}", exports.ToString());
        sourceBuilder.Replace("{interfaceName}", interfaceName);
        sourceBuilder.Replace("{delegateCount}", delegateCount.ToString());
        sourceBuilder.Replace("{functionPointers}", functionPointers.ToString());
        sourceBuilder.Replace("{invokerFunctions}", invokerFunctions.ToString());
        sourceBuilder.Replace("{invokerName}", invokerName);
        // File.WriteAllText(Path.Combine(@"E:\temp", symbol.Name + ".g.cs"), sourceBuilder.ToString());

        return ($"{symbol.ContainingNamespace?.Name ?? "_"}.{symbol.Name}", sourceBuilder.ToString());
    }
}
#pragma warning restore SA1118 // Parameter should not span multiple lines
