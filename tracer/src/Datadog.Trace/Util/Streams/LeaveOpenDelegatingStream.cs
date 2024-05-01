// <copyright file="LeaveOpenDelegatingStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Streams;

/// <summary>
/// A simple <see cref="Stream"/> implementation that delegates to an inner stream implementation
/// but does not close the stream when disposed/closed
/// </summary>
internal abstract class LeaveOpenDelegatingStream(Stream innerStream) : DelegatingStream(innerStream)
{
    public override void Close()
    {
        // Override so that we don't dispose the inner stream
    }

    protected override void Dispose(bool disposing)
    {
        // Override so that we don't dispose the inner stream
    }

#if NETCOREAPP
    public override ValueTask DisposeAsync()
    {
        // Override so that we don't dispose the inner stream
        return default;
    }
#endif
}
