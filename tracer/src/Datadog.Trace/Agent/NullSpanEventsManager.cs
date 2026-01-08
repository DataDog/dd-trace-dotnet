// <copyright file="NullSpanEventsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent;

internal sealed class NullSpanEventsManager : ISpanEventsManager
{
    public bool NativeSpanEventsEnabled => false;

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
