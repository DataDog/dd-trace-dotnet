// <copyright file="MainViewModel.SideBar.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using ReactiveUI;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal partial class MainViewModel
{
    private string? _assemblyPath;
    private MethodDef? _selectedMethod;

    public ObservableCollection<Node> Items { get; private set; } = new();

    public ObservableCollection<Node> SelectedItems { get; private set; } = new();

    public MethodDef? SelectedMethod
    {
        get => _selectedMethod;
        private set => this.RaiseAndSetIfChanged(ref _selectedMethod, value);
    }

    public string? AssemblyPath
    {
        get => _assemblyPath;
        private set => this.RaiseAndSetIfChanged(ref _assemblyPath, value);
    }

    private void InitSideBar()
    {
        SelectedItems.CollectionChanged += (sender, args) =>
        {
            if (args.NewItems is { Count: > 0 } items && items[0] is Node { Definition: MethodDef methodDef })
            {
                SelectedMethod = methodDef;
            }
            else
            {
                SelectedMethod = null;
            }
        };
    }

    public void LoadAssembly(Stream assemblyStream)
    {
        Items.Clear();
        SelectedItems.Clear();
        var assemblyDef = AssemblyDef.Load(assemblyStream);
        Items.Add(new Node(assemblyDef));
    }

    public class Node
    {
        public Node(IDnlibDef dnlibDef)
        {
            Definition = dnlibDef;
            FullName = dnlibDef.FullName;
            Children = GetChildren(Definition);
        }

        public ObservableCollection<Node>? Children { get; set; }

        public string FullName { get; }

        public IDnlibDef Definition { get; }

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

                var node = new Node(item)
                {
                    Children = childrenOfNode
                };

                children ??= new ObservableCollection<Node>();
                children.Add(node);
            }
        }
    }
}
