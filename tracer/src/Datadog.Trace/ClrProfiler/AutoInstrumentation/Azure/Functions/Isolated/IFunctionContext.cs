// <copyright file="IFunctionContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Duck type for https://github.com/Azure/azure-functions-dotnet-worker/blob/0fd7bf6aef005e4b8a14874506bf7a8ad7ad73ef/src/DotNetWorker.Core/Context/FunctionContext.cs
/// </summary>
internal interface IFunctionContext
{
    FunctionDefinitionStruct FunctionDefinition { get; }

    IEnumerable<KeyValuePair<Type, object?>>? Features { get; }
}

#endif
