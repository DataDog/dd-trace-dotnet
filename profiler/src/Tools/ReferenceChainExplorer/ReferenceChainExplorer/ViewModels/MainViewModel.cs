// <copyright file="MainViewModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using ReferenceChainExplorer.Settings;
using ReferenceChainModel;

namespace ReferenceChainExplorer.ViewModels;

/// <summary>
/// Main ViewModel for the Reference Chain Explorer.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private ReferenceTree? _tree;
    private TypeSummary? _selectedType;
    private IReadOnlyList<ForwardTreeNode> _forwardTreeRoots = Array.Empty<ForwardTreeNode>();
    private string _statusText = "No file loaded";
    private string? _lastLoadedDirectory;
    private string _typeSearchText = string.Empty;
    private string _forwardTreeFilterText = string.Empty;
    private DispatcherTimer? _forwardTreeFilterDebounce;

    public MainViewModel()
    {
        TypeSummaries = new ObservableCollection<TypeSummary>();
        ReverseChains = new ObservableCollection<ReverseChainNode>();
        LoadCommand = new RelayCommand(LoadFile);
        ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());

        var settings = UserSettingsStore.Load();
        _lastLoadedDirectory = settings.LastJsonFolder;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Fired when a new file is loaded so the View can update the converter's TypeTable.
    /// </summary>
    public event EventHandler? FileLoaded;

    /// <summary>
    /// Gets the type summaries for the master list. Sorted by Count descending by default.
    /// </summary>
    public ObservableCollection<TypeSummary> TypeSummaries { get; }

    /// <summary>
    /// Gets or sets the currently selected type summary.
    /// Setting this triggers reverse chain computation.
    /// </summary>
    public TypeSummary? SelectedType
    {
        get => _selectedType;
        set
        {
            if (_selectedType != value)
            {
                _selectedType = value;
                OnPropertyChanged();
                UpdateReverseChains();
            }
        }
    }

    /// <summary>
    /// Gets the reverse chains for the currently selected type.
    /// </summary>
    public ObservableCollection<ReverseChainNode> ReverseChains { get; }

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
    /// Gets the command to load a JSON file.
    /// </summary>
    public ICommand LoadCommand { get; }

    /// <summary>
    /// Gets the command to exit the application.
    /// </summary>
    public ICommand ExitCommand { get; }

    /// <summary>
    /// Gets the currently loaded reference tree (used by MainWindow for converter setup).
    /// </summary>
    public ReferenceTree? Tree => _tree;

    /// <summary>
    /// Search text to filter the type list. Match is case-insensitive on short type names.
    /// </summary>
    public string TypeSearchText
    {
        get => _typeSearchText;
        set
        {
            if (_typeSearchText != value)
            {
                _typeSearchText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Filter text for the forward reference tree. Only paths containing a matching type are shown.
    /// Match is case-insensitive on short type names. Applied after 300ms debounce.
    /// </summary>
    public string ForwardTreeFilterText
    {
        get => _forwardTreeFilterText;
        set
        {
            if (_forwardTreeFilterText != value)
            {
                _forwardTreeFilterText = value ?? string.Empty;
                OnPropertyChanged();
                DebounceForwardTreeUpdate();
            }
        }
    }

    /// <summary>
    /// Gets the forward reference tree rooted by category (Stack, Handle, etc.).
    /// Replaced when a new file is loaded.
    /// </summary>
    public IReadOnlyList<ForwardTreeNode> ForwardTreeRoots
    {
        get => _forwardTreeRoots;
        private set
        {
            if (_forwardTreeRoots != value)
            {
                _forwardTreeRoots = value;
                OnPropertyChanged();
            }
        }
    }

    private void LoadFile(object? parameter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Open Reference Chain JSON",
            InitialDirectory = _lastLoadedDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _tree = ReferenceTreeLoader.LoadFromFile(dialog.FileName);

            TypeSummaries.Clear();
            ReverseChains.Clear();
            _selectedType = null;
            TypeSearchText = string.Empty;
            ForwardTreeFilterText = string.Empty;
            OnPropertyChanged(nameof(SelectedType));

            UpdateForwardTree();

            var summaries = TypeSummary.BuildFromTree(_tree);
            foreach (var summary in summaries.OrderByDescending(s => s.TotalInstanceCount))
            {
                TypeSummaries.Add(summary);
            }

            StatusText = $"Loaded: {_tree.TypeTable.Count} types, {_tree.Roots.Count} roots — {dialog.FileName}";

            var directory = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                _lastLoadedDirectory = directory;
                UserSettingsStore.Save(new UserSettings { LastJsonFolder = directory });
            }

            // Notify the View to update the converter's TypeTable
            FileLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading file: {ex.Message}";
            ForwardTreeRoots = Array.Empty<ForwardTreeNode>();
        }
    }

    private void UpdateReverseChains()
    {
        ReverseChains.Clear();

        if (_tree is null || _selectedType is null)
        {
            return;
        }

        var chains = ReverseChainBuilder.Build(_tree, _selectedType.TypeIndex);
        foreach (var node in chains)
        {
            ReverseChains.Add(node);
        }
    }

    private void DebounceForwardTreeUpdate()
    {
        _forwardTreeFilterDebounce?.Stop();

        _forwardTreeFilterDebounce = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _forwardTreeFilterDebounce.Tick += (_, _) =>
        {
            _forwardTreeFilterDebounce.Stop();
            _forwardTreeFilterDebounce = null;
            UpdateForwardTree();
        };
        _forwardTreeFilterDebounce.Start();
    }

    private void UpdateForwardTree()
    {
        _forwardTreeFilterDebounce?.Stop();
        _forwardTreeFilterDebounce = null;

        if (_tree is null)
        {
            return;
        }

        var filter = string.IsNullOrWhiteSpace(_forwardTreeFilterText) ? null : _forwardTreeFilterText;
        ForwardTreeRoots = ForwardTreeBuilder.BuildFiltered(_tree, filter);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
