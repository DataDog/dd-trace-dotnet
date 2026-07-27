// <copyright file="GenerationConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using dnlib.DotNet;

namespace Datadog.AutoInstrumentation.Generator.Core;

/// <summary>
/// Configuration for code generation. Plain POCO with all boolean flags
/// that control what gets generated.
/// </summary>
public class GenerationConfiguration
{
    // OnMethod handlers
    public bool CreateOnMethodBegin { get; set; } = true;

    public bool CreateOnMethodEnd { get; set; } = true;

    public bool CreateOnAsyncMethodEnd { get; set; }

    // Duck typing general
    public bool UseDuckCopyStruct { get; set; }

    // Duck type: Instance
    public bool CreateDucktypeInstance { get; set; }

    public bool DucktypeInstanceFields { get; set; }

    public bool DucktypeInstanceProperties { get; set; } = true;

    public bool DucktypeInstanceMethods { get; set; }

    public bool DucktypeInstanceDuckChaining { get; set; }

    // Duck type: Arguments
    public bool CreateDucktypeArguments { get; set; }

    public bool DucktypeArgumentsFields { get; set; }

    public bool DucktypeArgumentsProperties { get; set; } = true;

    public bool DucktypeArgumentsMethods { get; set; }

    public bool DucktypeArgumentsDuckChaining { get; set; }

    // Duck type: Return Value
    public bool CreateDucktypeReturnValue { get; set; }

    public bool DucktypeReturnValueFields { get; set; }

    public bool DucktypeReturnValueProperties { get; set; } = true;

    public bool DucktypeReturnValueMethods { get; set; }

    public bool DucktypeReturnValueDuckChaining { get; set; }

    // Duck type: Async Return Value
    public bool CreateDucktypeAsyncReturnValue { get; set; }

    public bool DucktypeAsyncReturnValueFields { get; set; }

    public bool DucktypeAsyncReturnValueProperties { get; set; } = true;

    public bool DucktypeAsyncReturnValueMethods { get; set; }

    public bool DucktypeAsyncReturnValueDuckChaining { get; set; }

    /// <summary>
    /// Returns true if the given return type full name is an async return type
    /// that CallTarget instruments via OnAsyncMethodEnd: Task, Task&lt;T&gt;, ValueTask,
    /// or ValueTask&lt;T&gt;. Other types under System.Threading.Tasks (TaskScheduler,
    /// TaskCompletionSource, TaskFactory, etc.) are not async return shapes.
    /// dnlib formats generic full names as `System.Threading.Tasks.Task`1&lt;T&gt;`.
    /// </summary>
    public static bool IsAsyncReturnType(string returnTypeFullName)
    {
        return returnTypeFullName == "System.Threading.Tasks.Task"
            || returnTypeFullName == "System.Threading.Tasks.ValueTask"
            || returnTypeFullName.StartsWith("System.Threading.Tasks.Task`1<", StringComparison.Ordinal)
            || returnTypeFullName.StartsWith("System.Threading.Tasks.ValueTask`1<", StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a configuration with smart defaults based on the method being instrumented.
    /// Mirrors the GUI auto-detection logic from MainViewModel.Configuration.cs.
    /// </summary>
    public static GenerationConfiguration CreateForMethod(MethodDef methodDef)
    {
        var config = new GenerationConfiguration();

        var isAsync = IsAsyncReturnType(methodDef.ReturnType.FullName);

        if (isAsync)
        {
            config.CreateOnMethodEnd = false;
            config.CreateDucktypeReturnValue = false;
            config.CreateOnAsyncMethodEnd = true;
        }
        else
        {
            config.CreateOnMethodEnd = true;
            config.CreateOnAsyncMethodEnd = false;
            config.CreateDucktypeAsyncReturnValue = false;
        }

        if (methodDef.IsStatic || (!config.CreateOnMethodBegin && !config.CreateOnMethodEnd && !config.CreateOnAsyncMethodEnd))
        {
            config.CreateDucktypeInstance = false;
        }

        return config;
    }
}
