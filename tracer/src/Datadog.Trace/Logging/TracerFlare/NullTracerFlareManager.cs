// <copyright file="NullTracerFlareManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Logging.TracerFlare;

internal class NullTracerFlareManager : ITracerFlareManager
{
    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
