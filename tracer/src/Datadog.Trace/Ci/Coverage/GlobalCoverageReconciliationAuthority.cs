// <copyright file="GlobalCoverageReconciliationAuthority.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageReconciliationAuthority : IDisposable
{
    private FileStream? _claimStream;

    internal GlobalCoverageReconciliationAuthority(string claimPath, FileStream claimStream)
    {
        ClaimPath = claimPath;
        _claimStream = claimStream;
    }

    internal string ClaimPath { get; }

    internal FileStream ClaimStream
        => Volatile.Read(ref _claimStream) ?? throw new ObjectDisposedException(nameof(GlobalCoverageReconciliationAuthority));

    internal void Complete()
    {
        var stream = Interlocked.Exchange(ref _claimStream, null) ?? throw new ObjectDisposedException(nameof(GlobalCoverageReconciliationAuthority));
        stream.Dispose();
        File.Delete(ClaimPath);
    }

    public void Dispose()
        => Interlocked.Exchange(ref _claimStream, null)?.Dispose();
}
