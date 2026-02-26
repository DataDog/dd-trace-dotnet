// <copyright file="MainViewModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http.Headers;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace Datadog.AutoInstrumentation.Generator.ViewModels
{
    internal partial class MainViewModel : ViewModelBase
    {
        public MainViewModel()
        {
            InitConfiguration();
            InitSideBar();
            InitEditor();

            OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
        }

        public static Window GetCurrentWindow
        {
            get
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                    {
                        MainWindow: { } mainWindow
                    })
                {
                    return mainWindow;
                }

                throw new NullReferenceException("MainWindow is null");
            }
        }

        public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

        [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "This method await for windows dialogs so should be keep the synchronization context")]
        private async Task OpenFileAsync()
        {
            var files = await GetCurrentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Open an assembly file",
                FileTypeFilter = new[] { new FilePickerFileType("Assembly file") { Patterns = new[] { "*.dll" } } }
            });

            if (files?.Count > 0 && files[0] is { } file)
            {
                await OpenFileAsync(file);
            }
        }

        [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "This method await for windows dialogs so should be keep the synchronization context")]
        private async Task OpenFileAsync(IStorageFile file)
        {
            if (file.TryGetLocalPath() is { } filePath)
            {
                await OpenFileAsync(filePath);
            }
            else
            {
                var window = GetCurrentWindow;
                await MessageBoxManager.GetMessageBoxStandard(
                    window.Title,
                    $"Error opening assembly: {file.Name}",
                    ButtonEnum.Ok,
                    Icon.Error,
                    WindowStartupLocation.CenterOwner).ShowWindowDialogAsync(window);
            }
        }

        [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "This method await for windows dialogs so should be keep the synchronization context")]
        private async Task OpenFileAsync(string filePath)
        {
            var window = GetCurrentWindow;

            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".dll":
                    try
                    {
                        var assemblyStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        AssemblyPath = filePath;
                        LoadAssembly(assemblyStream);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandard(
                            window.Title,
                            $"Error opening assembly: {filePath}\n{ex.Message}",
                            ButtonEnum.Ok,
                            Icon.Error,
                            WindowStartupLocation.CenterOwner).ShowWindowDialogAsync(window);
                    }

                    break;
                default:
                    await MessageBoxManager.GetMessageBoxStandard(
                        window.Title,
                        $"Error opening file: {filePath} is not an Assembly file (.dll)",
                        ButtonEnum.Ok,
                        Icon.Error,
                        WindowStartupLocation.CenterOwner).ShowWindowDialogAsync(window);
                    break;
            }
        }
    }
}
