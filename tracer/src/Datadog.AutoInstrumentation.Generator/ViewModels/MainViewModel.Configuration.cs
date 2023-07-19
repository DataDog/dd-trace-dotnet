// <copyright file="MainViewModel.Configuration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using ReactiveUI;

namespace Datadog.AutoInstrumentation.Generator.ViewModels;

internal partial class MainViewModel
{
    private bool _createOnMethodBegin = true;
    private bool _createOnMethodBeginDucktypeInstance;
    private bool _createOnMethodBeginDucktypeArguments;

    private bool _createOnMethodEnd = true;
    private bool _createOnMethodEndDucktypeInstance;
    private bool _createOnMethodEndDucktypeReturnValue;

    private bool _createOnAsyncMethodEndIsVisible = false;
    private bool _createOnAsyncMethodEnd;
    private bool _createOnAsyncMethodEndDucktypeInstance;
    private bool _createOnAsyncMethodEndDucktypeReturnValue;

    public bool CreateOnMethodBegin
    {
        get => _createOnMethodBegin;
        set => this.RaiseAndSetIfChanged(ref _createOnMethodBegin, value);
    }

    public bool CreateOnMethodBeginDucktypeInstance
    {
        get => _createOnMethodBeginDucktypeInstance;
        set
        {
            if (value && SelectedMethod?.IsStatic == true)
            {
                this.RaiseAndSetIfChanged(ref _createOnMethodBeginDucktypeInstance, false);
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _createOnMethodBeginDucktypeInstance, value);
            }
        }
    }

    public bool CreateOnMethodBeginDucktypeArguments
    {
        get => _createOnMethodBeginDucktypeArguments;
        set => this.RaiseAndSetIfChanged(ref _createOnMethodBeginDucktypeArguments, value);
    }

    public bool CreateOnMethodEnd
    {
        get => _createOnMethodEnd;
        set => this.RaiseAndSetIfChanged(ref _createOnMethodEnd, value);
    }

    public bool CreateOnMethodEndDucktypeInstance
    {
        get => _createOnMethodEndDucktypeInstance;
        set
        {
            if (value && SelectedMethod?.IsStatic == true)
            {
                this.RaiseAndSetIfChanged(ref _createOnMethodEndDucktypeInstance, false);
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _createOnMethodEndDucktypeInstance, value);
            }
        }
    }

    public bool CreateOnMethodEndDucktypeReturnValue
    {
        get => _createOnMethodEndDucktypeReturnValue;
        set => this.RaiseAndSetIfChanged(ref _createOnMethodEndDucktypeReturnValue, value);
    }

    public bool CreateOnAsyncMethodEndIsVisible
    {
        get => _createOnAsyncMethodEndIsVisible;
        set => this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEndIsVisible, value);
    }

    public bool CreateOnAsyncMethodEnd
    {
        get => _createOnAsyncMethodEnd;
        set => this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEnd, value);
    }

    public bool CreateOnAsyncMethodEndDucktypeInstance
    {
        get => _createOnAsyncMethodEndDucktypeInstance;
        set
        {
            if (value && SelectedMethod?.IsStatic == true)
            {
                this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEndDucktypeInstance, false);
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEndDucktypeInstance, value);
            }
        }
    }

    public bool CreateOnAsyncMethodEndDucktypeReturnValue
    {
        get => _createOnAsyncMethodEndDucktypeReturnValue;
        set => this.RaiseAndSetIfChanged(ref _createOnAsyncMethodEndDucktypeReturnValue, value);
    }

    private void InitConfiguration()
    {
        this.WhenAnyValue(o => o.SelectedMethod).Subscribe(methodDef =>
        {
            if (methodDef is null)
            {
                return;
            }

            if (methodDef.IsStatic)
            {
                CreateOnMethodBeginDucktypeInstance = false;
                CreateOnMethodEndDucktypeInstance = false;
                CreateOnAsyncMethodEndDucktypeInstance = false;
            }

            if (methodDef.ReturnType.FullName.StartsWith(typeof(Task).FullName!, StringComparison.Ordinal) ||
                methodDef.ReturnType.FullName.StartsWith(typeof(ValueTask).FullName!, StringComparison.Ordinal))
            {
                CreateOnAsyncMethodEndIsVisible = true;
                CreateOnMethodEnd = false;
                CreateOnAsyncMethodEnd = true;
            }
            else
            {
                CreateOnMethodEnd = true;
                CreateOnAsyncMethodEndIsVisible = false;
                CreateOnAsyncMethodEnd = false;
                CreateOnAsyncMethodEndDucktypeInstance = false;
                CreateOnAsyncMethodEndDucktypeReturnValue = false;
            }
        });
    }
}
