// <copyright file="MainWindow.xaml.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using EEHeapExplorer.ViewModels;

namespace EEHeapExplorer;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Mirror the top grid's column widths into the view model so the grouped header cells (which
        // bind through WidthsProxy) stay aligned with the data columns and follow resizing. We source
        // the widths from the real column-header elements, whose ActualWidth is a notifying size that
        // reflects the settled layout and updates whenever a column is resized.
        Loaded += (_, _) => HookColumnHeaders();

        // Optional: auto-load a report passed on the command line (e.g. EEHeapExplorer.exe report.json).
        // Deferred until after the first render so the grid's columns are fully laid out before data
        // arrives (loading during Loaded corrupts the initial column layout).
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            ContentRendered += (_, _) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.LoadFromPath(args[1]);
                }
            };
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void HookColumnHeaders()
    {
        foreach (var header in FindVisualChildren<DataGridColumnHeader>(KindGrid))
        {
            header.SizeChanged -= OnHeaderSizeChanged;
            header.SizeChanged += OnHeaderSizeChanged;
        }

        SyncColumnWidths();
    }

    private void OnHeaderSizeChanged(object sender, SizeChangedEventArgs e) => SyncColumnWidths();

    private void SyncColumnWidths()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var widths = vm.ColumnWidths;
        widths.Kind = KindColumn.ActualWidth;
        widths.Count = CountColumn.ActualWidth;
        widths.Reserved = ReservedColumn.ActualWidth;
        widths.Committed = CommittedColumn.ActualWidth;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}
