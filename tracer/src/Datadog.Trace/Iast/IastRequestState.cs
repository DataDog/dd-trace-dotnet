// <copyright file="IastRequestState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Iast;

/// <summary>
/// The state of an IAST request: whether it is being analyzed (Context is not null) and the information needed to end it.
/// </summary>
internal sealed class IastRequestState
{
    private int _ended;

    internal IastRequestState(Span? rootSpan, IastRequestContext? context)
    {
        RootSpan = rootSpan;
        Context = context;
    }

    internal Span? RootSpan { get; }

    internal IastRequestContext? Context { get; }

    internal bool TryEnd() => Interlocked.Exchange(ref _ended, 1) == 0;
}
