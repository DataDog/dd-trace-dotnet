// <copyright file="MainViewModel.SideBar.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.ObjectModel;
using System.IO;
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
}
