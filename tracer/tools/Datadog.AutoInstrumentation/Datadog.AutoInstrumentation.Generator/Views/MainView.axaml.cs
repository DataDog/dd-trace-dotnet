// <copyright file="MainView.axaml.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.TextMate;
using Datadog.AutoInstrumentation.Generator.ViewModels;
using ReactiveUI;
using TextMateSharp.Grammars;

namespace Datadog.AutoInstrumentation.Generator.Views
{
    internal partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();

            textEditor.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;
            textEditor.ShowLineNumbers = true;
            /*
            _textEditor.ContextMenu = new ContextMenu
            {
                ItemsSource = new List<MenuItem>
                {
                    new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) },
                    new MenuItem { Header = "Paste", InputGesture = new KeyGesture(Key.V, KeyModifiers.Control) },
                    new MenuItem { Header = "Cut", InputGesture = new KeyGesture(Key.X, KeyModifiers.Control) }
                }
            };
            */
            textEditor.Options.ShowBoxForControlCharacters = true;
            textEditor.Options.ColumnRulerPositions = new List<int> { 120 };
            textEditor.Options.ShowColumnRulers = true;
            textEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(textEditor.Options);
            textEditor.TextArea.RightClickMovesCaret = true;

            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

            var textMateInstallation = textEditor.InstallTextMate(registryOptions);
            var csharpLanguage = registryOptions.GetLanguageByExtension(".cs");

            AddHandler(
                PointerWheelChangedEvent,
                (o, i) =>
                {
                    if (i.KeyModifiers != KeyModifiers.Control)
                    {
                        return;
                    }

                    if (i.Delta.Y > 0)
                    {
                        textEditor.FontSize++;
                    }
                    else
                    {
                        textEditor.FontSize = textEditor.FontSize > 1 ? textEditor.FontSize - 1 : 1;
                    }
                },
                RoutingStrategies.Bubble,
                true);

            textEditor.Document = new TextDocument();
            textEditor.IsReadOnly = true;
            textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(csharpLanguage.Id));

            DataContextChanged += (sender, args) =>
            {
                if (DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.WhenAnyValue(o => o.SourceCode)
                        .Subscribe(source => textEditor.Document.Text = source);
                }
            };
        }
    }
}
