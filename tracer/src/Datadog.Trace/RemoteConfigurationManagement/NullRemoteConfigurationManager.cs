// <copyright file="NullRemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;

#nullable enable

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed class NullRemoteConfigurationManager : IRemoteConfigurationManager
{
    public bool IsEnabled => false;

    public Task? StopRequested => null;

    public void Dispose()
    {
    }

    public void Start(TracerManager owner)
    {
    }

    public Func<Task>? PrepareRetirement(out bool drainAtProcessExit)
    {
        drainAtProcessExit = false;
        return null;
    }

    public Func<Task>? PrepareShutdownDrain() => null;
}
