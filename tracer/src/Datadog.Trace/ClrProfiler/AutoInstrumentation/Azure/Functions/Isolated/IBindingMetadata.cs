// <copyright file="IBindingMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for binding metadata in Azure Functions
/// Represents https://github.com/Azure/azure-functions-dotnet-worker/blob/main/src/DotNetWorker.Core/FunctionMetadata/BindingMetadata.cs
/// </summary>
internal interface IBindingMetadata
{
    string? Type { get; }
}

#endif
