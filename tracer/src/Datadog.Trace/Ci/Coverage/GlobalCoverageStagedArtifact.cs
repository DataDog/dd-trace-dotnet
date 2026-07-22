// <copyright file="GlobalCoverageStagedArtifact.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageStagedArtifact : IDisposable
{
    private readonly string _destinationPath;
    private readonly bool _replaceExisting;
    private string? _temporaryPath;

    internal GlobalCoverageStagedArtifact(string temporaryPath, string destinationPath, bool replaceExisting)
    {
        _temporaryPath = temporaryPath;
        _destinationPath = destinationPath;
        _replaceExisting = replaceExisting;
    }

    internal void Commit()
    {
        var temporaryPath = _temporaryPath ?? throw new InvalidOperationException("The staged global coverage artifact is no longer available.");
        if (_replaceExisting && File.Exists(_destinationPath))
        {
            File.Replace(temporaryPath, _destinationPath, null);
        }
        else
        {
            File.Move(temporaryPath, _destinationPath);
        }

        _temporaryPath = null;
    }

    public void Dispose()
    {
        var temporaryPath = _temporaryPath;
        _temporaryPath = null;
        if (temporaryPath is null)
        {
            return;
        }

        try
        {
            File.Delete(temporaryPath);
        }
        catch
        {
            // A staged artifact is never a valid input and cleanup must not hide the primary failure.
        }
    }
}
