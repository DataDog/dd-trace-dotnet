// <copyright file="Node.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using dnlib.DotNet;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal class Node
{
    private static readonly IImage AssemblyIcon;
    private static readonly IImage ModuleIcon;
    private static readonly IImage NamespaceIcon;
    private static readonly IImage ClassAbstractIcon;
    private static readonly IImage ClassPrivateIcon;
    private static readonly IImage ClassPublicIcon;
    private static readonly IImage StructPrivateIcon;
    private static readonly IImage StructPublicIcon;
    private static readonly IImage InterfacePrivateIcon;
    private static readonly IImage InterfacePublicIcon;
    private static readonly IImage MethodInternalIcon;
    private static readonly IImage MethodPrivateIcon;
    private static readonly IImage MethodProtectedIcon;
    private static readonly IImage MethodPublicIcon;

    static Node()
    {
        AssemblyIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/Assembly.png")));
        ModuleIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/Module.png")));
        NamespaceIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/Namespace.png")));
        // .
        ClassAbstractIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/AbstractClass.png")));
        ClassPrivateIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/ClassPrivate.png")));
        ClassPublicIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/ClassPublic.png")));
        // .
        StructPrivateIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/StructurePrivate.png")));
        StructPublicIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/StructurePublic.png")));
        // .
        InterfacePrivateIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/InterfacePrivate.png")));
        InterfacePublicIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/InterfacePublic.png")));
        // .
        MethodInternalIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/MethodInternal.png")));
        MethodPrivateIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/MethodPrivate.png")));
        MethodProtectedIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/MethodProtected.png")));
        MethodPublicIcon = new Bitmap(AssetLoader.Open(new Uri("avares://Datadog.AutoInstrumentation.Generator/Assets/MethodPublic.png")));
    }

    public Node(object definition)
    {
        ReturnName = null;
        Arguments = null;
        Definition = definition;
        Children = GetChildren(definition);
        IconFile = null;
        switch (definition)
        {
            case AssemblyDef assemblyDef:
                Name = assemblyDef.FullName;
                Expanded = true;
                IconFile = AssemblyIcon;
                break;
            case ModuleDef moduleDef:
                Name = moduleDef.FullName;
                Expanded = true;
                IconFile = ModuleIcon;
                break;
            case TypeDef typeDef:
                Name = typeDef.Name;
                var isTypeAbstract = typeDef.IsAbstract;
                var isTypePublic = typeDef.IsPublic || typeDef.IsNestedPublic;
                if (typeDef.IsInterface)
                {
                    IconFile = isTypePublic ? InterfacePublicIcon : InterfacePrivateIcon;
                }
                else if (typeDef.IsValueType)
                {
                    IconFile = isTypePublic ? StructPublicIcon : StructPrivateIcon;
                }
                else
                {
                    IconFile = isTypePublic ? ClassPublicIcon : isTypeAbstract ? ClassAbstractIcon : ClassPrivateIcon;
                }

                break;
            case MethodDef methodDef:
                var sigString = methodDef.MethodSig.ToString() ?? "()";
                ReturnName = methodDef.MethodSig.RetType.FullName + " ";
                Name = methodDef.Name;
                Arguments = sigString.Substring(sigString.IndexOf('('));
                var isPublic = methodDef.IsPublic;
                var isInternal = methodDef is { IsPublic: false, IsAssembly: true };
                var isProtected = methodDef is { IsPublic: false, IsFamily: true };
                var isPrivate = methodDef is { IsPrivate: true, IsFamilyOrAssembly: false };
                if (isPublic)
                {
                    IconFile = MethodPublicIcon;
                }
                else if (isPrivate)
                {
                    IconFile = MethodPrivateIcon;
                }
                else if (isInternal)
                {
                    IconFile = MethodInternalIcon;
                }
                else if (isProtected)
                {
                    IconFile = MethodProtectedIcon;
                }

                break;
            case IGrouping<string, TypeDef> group:
                Name = group.Key;
                IconFile = NamespaceIcon;
                break;
            default:
                Name = string.Empty;
                break;
        }
    }

    public ObservableCollection<Node>? Children { get; set; }

    public string? ReturnName { get; }

    public string Name { get; }

    public string? Arguments { get; }

    public object? Definition { get; }

    public bool Expanded { get; }

    public IImage? IconFile { get; }

    private static ObservableCollection<Node>? GetChildren(object definition)
    {
        ObservableCollection<Node>? children = null;

        switch (definition)
        {
            case AssemblyDef assemblyDef:
            {
                foreach (var moduleDef in assemblyDef.Modules.OrderBy(m => m.FullName))
                {
                    FillChildren(ref children, moduleDef);
                }

                break;
            }

            case ModuleDef moduleDef:
            {
                var typesByGroup = moduleDef.Types
                                            .Where(t => !t.IsEnum)
                                            .OrderBy(t => t.FullName)
                                            .GroupBy(g => g.Namespace.String)
                                            .ToArray();

                foreach (var group in typesByGroup)
                {
                    if (string.IsNullOrEmpty(group.Key))
                    {
                        foreach (var typeDef in group)
                        {
                            FillChildren(ref children, typeDef);
                        }
                    }
                    else
                    {
                        FillChildren(ref children, group);
                    }
                }

                break;
            }

            case IGrouping<string, TypeDef> group:
            {
                foreach (var typeDef in group)
                {
                    FillChildren(ref children, typeDef);
                }

                break;
            }

            case TypeDef typeDef:
            {
                foreach (var nestedTypeDef in typeDef.NestedTypes.OrderBy(m => m.Name))
                {
                    FillChildren(ref children, nestedTypeDef);
                }

                foreach (var methodDef in typeDef.Methods.OrderBy(m => m.Name))
                {
                    FillChildren(ref children, methodDef);
                }

                break;
            }
        }

        return children;

        static void FillChildren(ref ObservableCollection<Node>? children, object item)
        {
            var childrenOfNode = GetChildren(item);
            if (item is not MethodDef && (childrenOfNode is null || childrenOfNode.Count == 0))
            {
                return;
            }

            var node = new Node(item) { Children = childrenOfNode };

            children ??= new ObservableCollection<Node>();
            children.Add(node);
        }
    }
}
