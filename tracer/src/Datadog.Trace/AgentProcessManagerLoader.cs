// <copyright file="AgentProcessManagerLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;

namespace Datadog.Trace;

/// <summary>
/// AgentProcessManager Loader.
/// Needs to be public as invoked from managed loader
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AgentProcessManagerLoader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProcessManagerLoader"/> class.
    /// </summary>
    public AgentProcessManagerLoader()
    {
        AgentProcessManager.Initialize();
    }
}
