// <copyright file="GlobalCoverageOutputRegistration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageOutputRegistration
{
    internal GlobalCoverageOutputRegistration(byte bit, string directory, string pendingPath, string readyPath, bool coordinator)
    {
        Bit = bit;
        Directory = directory;
        PendingPath = pendingPath;
        ReadyPath = readyPath;
        IsCoordinator = coordinator;
    }

    internal byte Bit { get; }

    internal string Directory { get; }

    internal string PendingPath { get; }

    internal string ReadyPath { get; }

    internal bool IsCoordinator { get; set; }
}
