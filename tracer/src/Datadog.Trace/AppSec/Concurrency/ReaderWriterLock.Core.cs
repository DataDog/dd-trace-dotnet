// <copyright file="ReaderWriterLock.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;

namespace Datadog.Trace.AppSec.Concurrency;

internal partial class ReaderWriterLock : IDisposable
{
    private readonly System.Threading.ReaderWriterLockSlim _readerWriterLock = new();

    internal void EnterReadLock() => _readerWriterLock.EnterReadLock();

    internal void EnterWriteLock() => _readerWriterLock.EnterWriteLock();

    internal void ExitReadLock() => _readerWriterLock.ExitReadLock();

    internal void ExitWriteLock() => _readerWriterLock.ExitWriteLock();

    public void Dispose() => _readerWriterLock.Dispose();
}
#endif
