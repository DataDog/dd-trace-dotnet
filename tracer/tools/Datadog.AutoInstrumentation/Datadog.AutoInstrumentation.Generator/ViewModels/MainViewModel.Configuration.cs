// <copyright file="MainViewModel.Configuration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal partial class MainViewModel
{
    private bool _createOnMethodBegin = true;
    private bool _createOnMethodEnd = true;
    private bool _createOnAsyncMethodEndIsVisible = false;
    private bool _createOnAsyncMethodEnd;

    private bool _useDuckCopyStruct;

    private bool _createDucktypeInstance;
    private bool _ducktypeInstanceFields = false;
    private bool _ducktypeInstanceProperties = true;
    private bool _ducktypeInstanceMethods = false;
    private bool _ducktypeInstanceDuckChaining = false;

    private bool _createDucktypeArguments;
    private bool _ducktypeArgumentsFields = false;
    private bool _ducktypeArgumentsProperties = true;
    private bool _ducktypeArgumentsMethods = false;
    private bool _ducktypeArgumentsDuckChaining = false;

    private bool _createDucktypeReturnValue;
    private bool _ducktypeReturnValueFields = false;
    private bool _ducktypeReturnValueProperties = true;
    private bool _ducktypeReturnValueMethods = false;
    private bool _ducktypeReturnValueDuckChaining = false;

    private bool _createDucktypeAsyncReturnValue;
    private bool _ducktypeAsyncReturnValueFields = false;
    private bool _ducktypeAsyncReturnValueProperties = true;
    private bool _ducktypeAsyncReturnValueMethods = false;
    private bool _ducktypeAsyncReturnValueDuckChaining = false;

    public bool CreateOnMethodBegin
    {
        get => _createOnMethodBegin;
        set
        {
            this.RaiseAndSetIfChanged(ref _createOnMethodBegin, value);
            this.RaisePropertyChanged(nameof(CreateDucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeArgumentsEnabled));
        }
    }

    public bool CreateOnMethodEnd
    {
        get => _createOnMethodEnd;
        set
        {
            this.RaiseAndSetIfChanged(ref _createOnMethodEnd, value);
            this.RaisePropertyChanged(nameof(CreateDucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeReturnValueEnabled));
        }
    }

    public bool CreateOnAsyncMethodEndIsVisible
    {
        get => _createOnAsyncMethodEndIsVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEndIsVisible, value);
            this.RaisePropertyChanged(nameof(CreateDucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeAsyncReturnValueEnabled));
        }
    }

    public bool CreateOnAsyncMethodEnd
    {
        get => _createOnAsyncMethodEnd;
        set
        {
            this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEnd, value);
            this.RaisePropertyChanged(nameof(CreateDucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeInstanceEnabled));
            this.RaisePropertyChanged(nameof(DucktypeAsyncReturnValueEnabled));
        }
    }

    public bool UseDuckCopyStruct
    {
        get => _useDuckCopyStruct;
        set => this.RaiseAndSetIfChanged(ref _useDuckCopyStruct, value);
    }

    // ...

    public bool CreateDucktypeInstanceEnabled
    {
        get => (CreateOnMethodBegin || CreateOnMethodEnd || CreateOnAsyncMethodEnd);
    }

    public bool CreateDucktypeInstance
    {
        get => _createDucktypeInstance;
        set
        {
            if (value && SelectedMethod?.IsStatic == true)
            {
                this.RaiseAndSetIfChanged(ref _createDucktypeInstance, false);
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _createDucktypeInstance, value);
            }

            this.RaisePropertyChanged(nameof(DucktypeInstanceEnabled));
        }
    }

    public bool DucktypeInstanceEnabled
    {
        get => CreateDucktypeInstance && CreateDucktypeInstanceEnabled;
    }

    public bool DucktypeInstanceFields
    {
        get => _ducktypeInstanceFields;
        set => this.RaiseAndSetIfChanged(ref _ducktypeInstanceFields, value);
    }

    public bool DucktypeInstanceProperties
    {
        get => _ducktypeInstanceProperties;
        set => this.RaiseAndSetIfChanged(ref _ducktypeInstanceProperties, value);
    }

    public bool DucktypeInstanceMethods
    {
        get => _ducktypeInstanceMethods;
        set => this.RaiseAndSetIfChanged(ref _ducktypeInstanceMethods, value);
    }

    public bool DucktypeInstanceDuckChaining
    {
        get => _ducktypeInstanceDuckChaining;
        set => this.RaiseAndSetIfChanged(ref _ducktypeInstanceDuckChaining, value);
    }

    // ...

    public bool CreateDucktypeArguments
    {
        get => _createDucktypeArguments;
        set
        {
            if (value && SelectedMethod?.Parameters.Count(p => !p.IsHiddenThisParameter) == 0)
            {
                this.RaiseAndSetIfChanged(ref _createDucktypeArguments, false);
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _createDucktypeArguments, value);
            }

            this.RaisePropertyChanged(nameof(DucktypeArgumentsEnabled));
        }
    }

    public bool DucktypeArgumentsEnabled
    {
        get => CreateOnMethodBegin && CreateDucktypeArguments;
    }

    public bool DucktypeArgumentsFields
    {
        get => _ducktypeArgumentsFields;
        set => this.RaiseAndSetIfChanged(ref _ducktypeArgumentsFields, value);
    }

    public bool DucktypeArgumentsProperties
    {
        get => _ducktypeArgumentsProperties;
        set => this.RaiseAndSetIfChanged(ref _ducktypeArgumentsProperties, value);
    }

    public bool DucktypeArgumentsMethods
    {
        get => _ducktypeArgumentsMethods;
        set => this.RaiseAndSetIfChanged(ref _ducktypeArgumentsMethods, value);
    }

    public bool DucktypeArgumentsDuckChaining
    {
        get => _ducktypeArgumentsDuckChaining;
        set => this.RaiseAndSetIfChanged(ref _ducktypeArgumentsDuckChaining, value);
    }

    // ...

    public bool CreateDucktypeReturnValue
    {
        get => _createDucktypeReturnValue;
        set
        {
            if (value && SelectedMethod?.ReturnType.FullName == "System.Void")
            {
                this.RaiseAndSetIfChanged(ref _createDucktypeReturnValue, false);
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _createDucktypeReturnValue, value);
            }

            this.RaisePropertyChanged(nameof(DucktypeReturnValueEnabled));
        }
    }

    public bool DucktypeReturnValueEnabled
    {
        get => CreateOnMethodEnd && CreateDucktypeReturnValue;
    }

    public bool DucktypeReturnValueFields
    {
        get => _ducktypeReturnValueFields;
        set => this.RaiseAndSetIfChanged(ref _ducktypeReturnValueFields, value);
    }

    public bool DucktypeReturnValueProperties
    {
        get => _ducktypeReturnValueProperties;
        set => this.RaiseAndSetIfChanged(ref _ducktypeReturnValueProperties, value);
    }

    public bool DucktypeReturnValueMethods
    {
        get => _ducktypeReturnValueMethods;
        set => this.RaiseAndSetIfChanged(ref _ducktypeReturnValueMethods, value);
    }

    public bool DucktypeReturnValueDuckChaining
    {
        get => _ducktypeReturnValueDuckChaining;
        set => this.RaiseAndSetIfChanged(ref _ducktypeReturnValueDuckChaining, value);
    }

    // ...

    public bool CreateDucktypeAsyncReturnValue
    {
        get => _createDucktypeAsyncReturnValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _createDucktypeAsyncReturnValue, value);
            this.RaisePropertyChanged(nameof(DucktypeAsyncReturnValueEnabled));
        }
    }

    public bool DucktypeAsyncReturnValueEnabled
    {
        get => CreateDucktypeAsyncReturnValue && CreateOnAsyncMethodEnd && CreateOnAsyncMethodEndIsVisible;
    }

    public bool DucktypeAsyncReturnValueFields
    {
        get => _ducktypeAsyncReturnValueFields;
        set => this.RaiseAndSetIfChanged(ref _ducktypeAsyncReturnValueFields, value);
    }

    public bool DucktypeAsyncReturnValueProperties
    {
        get => _ducktypeAsyncReturnValueProperties;
        set => this.RaiseAndSetIfChanged(ref _ducktypeAsyncReturnValueProperties, value);
    }

    public bool DucktypeAsyncReturnValueMethods
    {
        get => _ducktypeAsyncReturnValueMethods;
        set => this.RaiseAndSetIfChanged(ref _ducktypeAsyncReturnValueMethods, value);
    }

    public bool DucktypeAsyncReturnValueDuckChaining
    {
        get => _ducktypeAsyncReturnValueDuckChaining;
        set => this.RaiseAndSetIfChanged(ref _ducktypeAsyncReturnValueDuckChaining, value);
    }

    // ...

    private void InitConfiguration()
    {
        this.WhenAnyValue(o => o.SelectedMethod).Subscribe(methodDef =>
        {
            if (methodDef is null)
            {
                return;
            }

            if (methodDef.ReturnType.FullName.StartsWith(typeof(Task).FullName!, StringComparison.Ordinal) ||
                methodDef.ReturnType.FullName.StartsWith(typeof(ValueTask).FullName!, StringComparison.Ordinal))
            {
                CreateOnMethodEnd = false;
                CreateDucktypeReturnValue = false;
                CreateOnAsyncMethodEndIsVisible = true;
                CreateOnAsyncMethodEnd = true;
            }
            else
            {
                CreateOnMethodEnd = true;
                CreateOnAsyncMethodEndIsVisible = false;
                CreateOnAsyncMethodEnd = false;
                CreateDucktypeAsyncReturnValue = false;
            }

            if (methodDef.IsStatic || (!CreateOnMethodBegin && !CreateOnMethodEnd && !CreateOnAsyncMethodEnd))
            {
                CreateDucktypeInstance = false;
            }
        });
    }
}
