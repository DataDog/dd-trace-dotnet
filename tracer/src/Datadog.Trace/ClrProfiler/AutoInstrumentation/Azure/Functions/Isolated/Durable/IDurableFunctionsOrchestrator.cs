// <copyright file="IDurableFunctionsOrchestrator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for the FunctionsOrchestrator and WrapperOrchestrator function-context fields.
/// </summary>
internal interface IDurableFunctionsOrchestrator
{
    [DuckField(Name = "functionContext")]
    IDurableFunctionContext FunctionContext { get; }
}

#endif
