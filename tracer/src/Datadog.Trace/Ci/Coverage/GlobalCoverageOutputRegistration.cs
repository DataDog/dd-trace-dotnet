// <copyright file="GlobalCoverageOutputRegistration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageOutputRegistration
{
    public GlobalCoverageOutputRegistration(byte bit, string directory, string pendingPath, string readyPath, bool coordinator)
    {
        Bit = bit;
        Directory = directory;
        PendingPath = pendingPath;
        ReadyPath = readyPath;
        IsCoordinator = coordinator;
    }

    public byte Bit { get; }

    public string Directory { get; }

    public string PendingPath { get; }

    public string ReadyPath { get; }

    public bool IsCoordinator { get; set; }
}
