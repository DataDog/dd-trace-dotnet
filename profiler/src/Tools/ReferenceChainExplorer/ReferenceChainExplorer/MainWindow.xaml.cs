// <copyright file="MainWindow.xaml.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
