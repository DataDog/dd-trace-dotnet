// <copyright file="MainViewModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using EEHeapExplorer.Settings;
using EEHeapModel;

namespace EEHeapExplorer.ViewModels;

/// <summary>
/// Main ViewModel for the EEHeap Explorer. Presents a top kind-summary grid and a bottom detail
/// grid for the selected kind.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private EEHeapReport? _report;
    private KindSummary? _selectedKind;
    private string _statusText = "No file loaded";
    private string? _lastLoadedDirectory;

    public MainViewModel()
    {
        KindSummaries = new ObservableCollection<KindSummary>();
        DetailRows = new ObservableCollection<HeapDetailRow>();
        LoadCommand = new RelayCommand(LoadFile);
        ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());

        KindSummariesView = CollectionViewSource.GetDefaultView(KindSummaries);
        KindSummariesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(KindSummary.Group)));

        // Order groups by their committed total (largest first); the Group key keeps each group's
        // rows contiguous when two groups happen to share the same total; then committed within group.
        KindSummariesView.SortDescriptions.Add(new SortDescription(nameof(KindSummary.GroupCommittedTotal), ListSortDirection.Descending));
        KindSummariesView.SortDescriptions.Add(new SortDescription(nameof(KindSummary.Group), ListSortDirection.Ascending));
        KindSummariesView.SortDescriptions.Add(new SortDescription(nameof(KindSummary.CommittedTotal), ListSortDirection.Descending));

        var settings = UserSettingsStore.Load();
        _lastLoadedDirectory = settings.LastFolder;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the per-kind summaries for the top grid (sorted by reserved size descending).
    /// </summary>
    public ObservableCollection<KindSummary> KindSummaries { get; }

    /// <summary>
    /// Gets the grouped + sorted view over <see cref="KindSummaries"/> (grouped by high-level group,
    /// ordered by group then committed size descending) bound by the top grid.
    /// </summary>
    public ICollectionView KindSummariesView { get; }

    /// <summary>
    /// Gets the live widths of the top grid's columns, kept in sync by the view so the grouped header
    /// cells stay aligned with the data columns (including when the user resizes a column).
    /// </summary>
    public ColumnWidths ColumnWidths { get; } = new();

    /// <summary>
    /// Gets the detail rows for the currently selected kind.
    /// </summary>
    public ObservableCollection<HeapDetailRow> DetailRows { get; }

    /// <summary>
    /// Gets or sets the currently selected kind. Setting it rebuilds the detail rows.
    /// </summary>
    public KindSummary? SelectedKind
    {
        get => _selectedKind;
        set
        {
            if (_selectedKind != value)
            {
                _selectedKind = value;
                OnPropertyChanged();
                UpdateDetailRows();
            }
        }
    }

    /// <summary>
    /// Gets or sets the status bar text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the command to load an eeheap file.
    /// </summary>
    public ICommand LoadCommand { get; }

    /// <summary>
    /// Gets the command to exit the application.
    /// </summary>
    public ICommand ExitCommand { get; }

    /// <summary>
    /// Loads an eeheap report directly from a file path (used by the Load command after the file
    /// dialog, and for command-line auto-loading on startup).
    /// </summary>
    public void LoadFromPath(string fileName)
    {
        try
        {
            _report = EEHeapLoader.LoadFromFile(fileName);

            KindSummaries.Clear();
            DetailRows.Clear();
            _selectedKind = null;
            OnPropertyChanged(nameof(SelectedKind));

            foreach (var summary in KindSummary.BuildFromReport(_report))
            {
                KindSummaries.Add(summary);
            }

            StatusText =
                $"Loaded ({_report.Source}): {_report.Heaps.Count} regions, " +
                $"reserved {_report.TotalReserved:N0} B, committed {_report.TotalCommitted:N0} B — {fileName}";

            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory))
            {
                _lastLoadedDirectory = directory;
                UserSettingsStore.Save(new UserSettings { LastFolder = directory });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading file: {ex.Message}";
            KindSummaries.Clear();
            DetailRows.Clear();
        }
    }

    private void LoadFile(object? parameter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "EEHeap files (*.json;*.zip)|*.json;*.zip|JSON files (*.json)|*.json|Zip archives (*.zip)|*.zip|All files (*.*)|*.*",
            Title = "Open eeheap report",
            InitialDirectory = _lastLoadedDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadFromPath(dialog.FileName);
    }

    private void UpdateDetailRows()
    {
        DetailRows.Clear();

        if (_report is null || _selectedKind is null)
        {
            return;
        }

        foreach (var row in HeapDetailBuilder.BuildForKind(_report, _selectedKind.Kind))
        {
            DetailRows.Add(row);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
