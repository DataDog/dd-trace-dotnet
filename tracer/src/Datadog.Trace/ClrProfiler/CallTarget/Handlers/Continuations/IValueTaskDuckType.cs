// <copyright file="IValueTaskDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETCOREAPP3_1_OR_GREATER
#nullable enable
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal interface IValueTaskDuckType
{
    bool IsCompletedSuccessfully { get; }

    Task AsTask();
}
#endif
