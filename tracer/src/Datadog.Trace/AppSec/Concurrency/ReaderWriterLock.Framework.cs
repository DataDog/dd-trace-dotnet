// <copyright file="ReaderWriterLock.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

#if NETFRAMEWORK
namespace Datadog.Trace.AppSec.Concurrency;

internal partial class ReaderWriterLock : IDisposable
{
    private const int Timeout = 3000;

    private readonly System.Threading.ReaderWriterLock _readerWriterLock = new();

    internal void EnterReadLock() => _readerWriterLock.AcquireReaderLock(Timeout);

    internal void EnterWriteLock() => _readerWriterLock.AcquireWriterLock(Timeout);

    internal void ExitReadLock() => _readerWriterLock.ReleaseReaderLock();

    internal void ExitWriteLock() => _readerWriterLock.ReleaseWriterLock();

    public void Dispose()
    {
    }
}
#endif
