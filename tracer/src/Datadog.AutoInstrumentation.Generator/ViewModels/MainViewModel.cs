// <copyright file="MainViewModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
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

            OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        }

        public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

        [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "This method await for windows dialogs so should be keep the synchronization context")]
        private async Task OpenFile()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            {
                var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "Open an assembly file",
                    FileTypeFilter = new[] { new FilePickerFileType("Assembly file") { Patterns = new[] { "*.dll" } } }
                });

                if (files?.Count > 0 && files[0] is { } file)
                {
                    try
                    {
                        var assemblyStream = await file.OpenReadAsync();
                        AssemblyPath = file.TryGetLocalPath();
                        LoadAssembly(assemblyStream);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxManager.GetMessageBoxStandard(
                            mainWindow.Title,
                            $"Error opening assembly: {file.TryGetLocalPath() ?? file.Name}\n{ex.Message}",
                            ButtonEnum.Ok,
                            Icon.Error,
                            WindowStartupLocation.CenterOwner).ShowWindowDialogAsync(mainWindow);
                    }
                }
            }
        }
    }
}
