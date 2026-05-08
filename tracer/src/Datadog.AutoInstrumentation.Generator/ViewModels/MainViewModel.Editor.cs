// <copyright file="MainViewModel.Editor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.AutoInstrumentation.Generator.Core;
using ReactiveUI;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal partial class MainViewModel
{
    private readonly InstrumentationGenerator _generator = new();
    private string _sourceCode = string.Empty;

    public string SourceCode
    {
        get => _sourceCode;
        private set => this.RaiseAndSetIfChanged(ref _sourceCode, value);
    }

    private void InitEditor()
    {
        var subscribeAction = (bool value) => UpdateSourceCode();
        this.WhenAnyValue(o => o.AssemblyPath).Subscribe(_ => UpdateSourceCode());
        this.WhenAnyValue(o => o.SelectedMethod).Subscribe(_ => UpdateSourceCode());
        this.WhenAnyValue(o => o.CreateOnMethodBegin).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnMethodEnd).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.CreateOnAsyncMethodEnd).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.UseDuckCopyStruct).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeInstance).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeInstanceDuckChaining).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeArguments).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeArgumentsDuckChaining).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeReturnValue).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeReturnValueDuckChaining).Subscribe(subscribeAction);

        this.WhenAnyValue(o => o.CreateDucktypeAsyncReturnValue).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueFields).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueProperties).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueMethods).Subscribe(subscribeAction);
        this.WhenAnyValue(o => o.DucktypeAsyncReturnValueDuckChaining).Subscribe(subscribeAction);
    }

    private void UpdateSourceCode()
    {
        if (SelectedMethod is { } methodDef)
        {
            var config = BuildConfigurationFromViewModel();
            var result = _generator.Generate(methodDef, config);
            SourceCode = result.Success ? result.SourceCode! : $"// {result.ErrorMessage}";
        }
        else
        {
            if (string.IsNullOrEmpty(AssemblyPath))
            {
                SourceCode = "// Open an assembly using the File icon button.";
            }
            else
            {
                SourceCode = "// Select a method to show the source code of the integration.";
            }
        }
    }

    private GenerationConfiguration BuildConfigurationFromViewModel()
    {
        return new GenerationConfiguration
        {
            CreateOnMethodBegin = CreateOnMethodBegin,
            CreateOnMethodEnd = CreateOnMethodEnd,
            CreateOnAsyncMethodEnd = CreateOnAsyncMethodEnd,
            UseDuckCopyStruct = UseDuckCopyStruct,
            CreateDucktypeInstance = CreateDucktypeInstance,
            DucktypeInstanceFields = DucktypeInstanceFields,
            DucktypeInstanceProperties = DucktypeInstanceProperties,
            DucktypeInstanceMethods = DucktypeInstanceMethods,
            DucktypeInstanceDuckChaining = DucktypeInstanceDuckChaining,
            CreateDucktypeArguments = CreateDucktypeArguments,
            DucktypeArgumentsFields = DucktypeArgumentsFields,
            DucktypeArgumentsProperties = DucktypeArgumentsProperties,
            DucktypeArgumentsMethods = DucktypeArgumentsMethods,
            DucktypeArgumentsDuckChaining = DucktypeArgumentsDuckChaining,
            CreateDucktypeReturnValue = CreateDucktypeReturnValue,
            DucktypeReturnValueFields = DucktypeReturnValueFields,
            DucktypeReturnValueProperties = DucktypeReturnValueProperties,
            DucktypeReturnValueMethods = DucktypeReturnValueMethods,
            DucktypeReturnValueDuckChaining = DucktypeReturnValueDuckChaining,
            CreateDucktypeAsyncReturnValue = CreateDucktypeAsyncReturnValue,
            DucktypeAsyncReturnValueFields = DucktypeAsyncReturnValueFields,
            DucktypeAsyncReturnValueProperties = DucktypeAsyncReturnValueProperties,
            DucktypeAsyncReturnValueMethods = DucktypeAsyncReturnValueMethods,
            DucktypeAsyncReturnValueDuckChaining = DucktypeAsyncReturnValueDuckChaining,
        };
    }
}
