// <copyright file="MainWindow.xaml.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ReferenceChainExplorer.Converters;
using ReferenceChainExplorer.ViewModels;
using ReferenceChainModel;

namespace ReferenceChainExplorer;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            vm.FileLoaded += OnFileLoaded;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.TypeSearchText))
                {
                    RefreshTypeFilter();
                }
            };
        }
    }

    private static bool FilterTypeSummary(object item, MainViewModel vm)
    {
        if (vm.Tree is null || item is not TypeSummary summary)
        {
            return true;
        }

        var search = vm.TypeSearchText;
        if (string.IsNullOrEmpty(search))
        {
            return true;
        }

        var shortName = ReferenceTree.GetShortTypeNameFromFullName(vm.Tree.GetTypeName(summary.TypeIndex));
        return shortName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// When a file is loaded, update the converter's TypeTable so bindings resolve names.
    /// Also apply default sort by TotalInstanceCount descending and set up the type search filter.
    /// </summary>
    private void OnFileLoaded(object? sender, EventArgs e)
    {
        if (sender is MainViewModel vm && vm.Tree is not null)
        {
            // Set the TypeTable on the converter
            var converter = (TypeIndexToNameConverter)FindResource("TypeNameConverter");
            converter.TypeTable = vm.Tree.TypeTable;

            // Refresh all bindings that use this converter
            TypeGrid.Items.Refresh();

            var view = CollectionViewSource.GetDefaultView(TypeGrid.ItemsSource);
            if (view is not null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("TotalInstanceCount", ListSortDirection.Descending));
                view.Filter = item => FilterTypeSummary(item, vm);
            }
        }
    }

    private void RefreshTypeFilter()
    {
        var view = CollectionViewSource.GetDefaultView(TypeGrid.ItemsSource);
        view?.Refresh();
    }

    /// <summary>
    /// Custom sorting for the Name column: resolves names from the TypeTable for comparison.
    /// For numeric columns, the DataGrid handles sorting natively.
    /// </summary>
    private void TypeGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column.SortMemberPath != "TypeIndex")
        {
            // Let the DataGrid handle numeric columns natively
            return;
        }

        // Custom sort for the Name column
        e.Handled = true;

        var converter = (TypeIndexToNameConverter)FindResource("TypeNameConverter");
        if (converter.TypeTable is null)
        {
            return;
        }

        var view = CollectionViewSource.GetDefaultView(TypeGrid.ItemsSource);
        if (view is not ListCollectionView listView)
        {
            return;
        }

        // Toggle sort direction
        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        e.Column.SortDirection = direction;

        var typeTable = converter.TypeTable;
        listView.CustomSort = new TypeNameComparer(typeTable, direction);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new Window
        {
            Title = "About",
            Width = 380,
            Height = 200,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Icon = Icon,
        };

        var icon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/ReferenceChainExplorer;component/Assets/ReferenceChainExplorer-icon.png")),
            Width = 64,
            Height = 64,
            Margin = new Thickness(0, 0, 16, 0),
        };

        var text = new TextBlock
        {
            Text = "Reference Chain Explorer\n\nNavigate reference chains from\nprofiler heap snapshots.",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
        };

        panel.Children.Add(icon);
        panel.Children.Add(text);
        aboutWindow.Content = panel;
        aboutWindow.ShowDialog();
    }

    private void ForwardTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (sender is not TreeView treeView)
        {
            return;
        }

        if (treeView.SelectedItem is not ForwardTreeNode selectedNode)
        {
            return;
        }

        var converter = (TypeIndexToNameConverter)FindResource("TypeNameConverter");
        if (converter.TypeTable is null)
        {
            return;
        }

        var sb = new StringBuilder();
        FormatSubtree(sb, selectedNode, converter.TypeTable, prefix: "", isLast: true, isRoot: true);
        Clipboard.SetText(sb.ToString());
        e.Handled = true;
    }

    private static void FormatSubtree(
        StringBuilder sb,
        ForwardTreeNode node,
        IReadOnlyList<string> typeTable,
        string prefix,
        bool isLast,
        bool isRoot)
    {
        if (!isRoot)
        {
            sb.Append(prefix);
            sb.Append(isLast ? "\u2514\u2500 " : "\u251C\u2500 ");
        }

        if (node.Kind == ForwardTreeNodeKind.Category)
        {
            sb.Append('[');
            sb.Append(RootCategoryHelper.GetCategoryName(node.CategoryCode ?? "?"));
            sb.AppendLine("]");
        }
        else
        {
            string typeName = node.TypeIndex >= 0 && node.TypeIndex < typeTable.Count
                ? typeTable[node.TypeIndex]
                : "?";

            sb.Append(typeName);

            if (node.InstanceCount > 0 || node.TotalSize > 0)
            {
                sb.Append($" ({node.InstanceCount:N0} instances, {node.TotalSize:N0} bytes)");
            }

            if (node.FieldName is not null)
            {
                sb.Append($" .{node.FieldName}");
            }

            if (node.Kind == ForwardTreeNodeKind.Root && node.CategoryCode is not null)
            {
                sb.Append($" [{RootCategoryHelper.GetCategoryName(node.CategoryCode)}]");
            }

            sb.AppendLine();
        }

        var children = node.Children;
        string childPrefix = isRoot ? "" : prefix + (isLast ? "   " : "\u2502  ");
        for (int i = 0; i < children.Count; i++)
        {
            FormatSubtree(sb, children[i], typeTable, childPrefix, i == children.Count - 1, isRoot: false);
        }
    }

    private void TypeGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        if (dataGrid.SelectedItem is not TypeSummary selectedType)
        {
            return;
        }

        var converter = (TypeIndexToNameConverter)FindResource("TypeNameConverter");
        if (converter.TypeTable is null)
        {
            return;
        }

        var typeTable = converter.TypeTable;
        string typeName = selectedType.TypeIndex >= 0 && selectedType.TypeIndex < typeTable.Count
            ? typeTable[selectedType.TypeIndex]
            : "?";

        var sb = new StringBuilder();
        sb.AppendLine($"{typeName} ({selectedType.TotalInstanceCount:N0} instances, {selectedType.TotalSize:N0} bytes)");

        if (DataContext is MainViewModel vm)
        {
            foreach (var chain in vm.ReverseChains)
            {
                sb.AppendLine();
                FormatReverseSubtree(sb, chain, typeTable, prefix: "", isLast: true, isRoot: true);
            }
        }

        Clipboard.SetText(sb.ToString());
        e.Handled = true;
    }

    private void ReverseTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (sender is not TreeView treeView)
        {
            return;
        }

        if (treeView.SelectedItem is not ReverseChainNode selectedNode)
        {
            return;
        }

        var converter = (TypeIndexToNameConverter)FindResource("TypeNameConverter");
        if (converter.TypeTable is null)
        {
            return;
        }

        var sb = new StringBuilder();
        FormatReverseSubtree(sb, selectedNode, converter.TypeTable, prefix: "", isLast: true, isRoot: true);
        Clipboard.SetText(sb.ToString());
        e.Handled = true;
    }

    private static void FormatReverseSubtree(
        StringBuilder sb,
        ReverseChainNode node,
        IReadOnlyList<string> typeTable,
        string prefix,
        bool isLast,
        bool isRoot)
    {
        if (!isRoot)
        {
            sb.Append(prefix);
            sb.Append(isLast ? "\u2514\u2500 " : "\u251C\u2500 ");
        }

        string typeName = node.TypeIndex >= 0 && node.TypeIndex < typeTable.Count
            ? typeTable[node.TypeIndex]
            : "?";

        sb.Append(typeName);
        sb.Append($" ({node.InstanceCount:N0} instances, {node.TotalSize:N0} bytes)");

        if (node.FieldName is not null)
        {
            sb.Append($" .{node.FieldName}");
        }

        if (node.IsRoot && node.CategoryCode is not null)
        {
            sb.Append($" [{RootCategoryHelper.GetCategoryNamesForDisplay(node.CategoryCode)}]");
        }

        sb.AppendLine();

        var children = node.Parents;
        string childPrefix = isRoot ? "" : prefix + (isLast ? "   " : "\u2502  ");
        for (int i = 0; i < children.Count; i++)
        {
            FormatReverseSubtree(sb, children[i], typeTable, childPrefix, i == children.Count - 1, isRoot: false);
        }
    }

    /// <summary>
    /// Compares TypeSummary objects by resolved type name.
    /// </summary>
    private class TypeNameComparer : System.Collections.IComparer
    {
        private readonly IReadOnlyList<string> _typeTable;
        private readonly ListSortDirection _direction;

        public TypeNameComparer(IReadOnlyList<string> typeTable, ListSortDirection direction)
        {
            _typeTable = typeTable;
            _direction = direction;
        }

        public int Compare(object? x, object? y)
        {
            if (x is TypeSummary a && y is TypeSummary b)
            {
                string nameA = ResolveName(a.TypeIndex);
                string nameB = ResolveName(b.TypeIndex);
                int result = string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                return _direction == ListSortDirection.Ascending ? result : -result;
            }

            return 0;
        }

        private string ResolveName(int index)
        {
            if (index >= 0 && index < _typeTable.Count)
            {
                return ReferenceTree.GetShortTypeNameFromFullName(_typeTable[index]);
            }

            return "?";
        }
    }
}
