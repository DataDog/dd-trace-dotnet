// <copyright file="Node.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.ObjectModel;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal class Node
{
    public Node(IDnlibDef dnlibDef)
    {
        Definition = dnlibDef;
        FullName = dnlibDef.FullName;
        Children = GetChildren(Definition);
        switch (dnlibDef)
        {
            case AssemblyDef:
                Expanded = true;
                IsAssembly = true;
                break;
            case ModuleDef:
                Expanded = true;
                IsModule = true;
                break;
            case TypeDef:
                IsType = true;
                break;
            case MethodDef:
                IsMethod = true;
                break;
        }
    }

    public ObservableCollection<Node>? Children { get; set; }

    public string FullName { get; }

    public IDnlibDef Definition { get; }

    public bool Expanded { get; }

    public bool IsAssembly { get; }

    public bool IsModule { get; }

    public bool IsType { get; }

    public bool IsMethod { get; }

    private static ObservableCollection<Node>? GetChildren(IDnlibDef definition)
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
                foreach (var typeDef in moduleDef.Types.OrderBy(t => t.FullName))
                {
                    if (typeDef.IsInterface)
                    {
                        continue;
                    }

                    FillChildren(ref children, typeDef);
                }

                break;
            }

            case TypeDef typeDef:
            {
                foreach (var methodDef in typeDef.Methods.OrderBy(m => m.Name))
                {
                    FillChildren(ref children, methodDef);
                }

                break;
            }
        }

        return children;

        static void FillChildren<T>(ref ObservableCollection<Node>? children, T item)
            where T : IDnlibDef
        {
            var childrenOfNode = GetChildren(item);
            if (typeof(T) != typeof(MethodDef) && (childrenOfNode is null || childrenOfNode.Count == 0))
            {
                return;
            }

            var node = new Node(item) { Children = childrenOfNode };

            children ??= new ObservableCollection<Node>();
            children.Add(node);
        }
    }
}
