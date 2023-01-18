// <copyright file="ReaderWriterLock.Core.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.AppSec.Concurrency;

internal partial class ReaderWriterLock : IDisposable
{
    private const int Timeout = 3000;

#if !NETFRAMEWORK
    private readonly System.Threading.ReaderWriterLockSlim _readerWriterLock = new();

    internal bool TryEnterReadLock() => _readerWriterLock.TryEnterReadLock(Timeout);

    internal bool TryEnterWriteLock() => _readerWriterLock.TryEnterWriteLock(Timeout);

    public void Dispose() => _readerWriterLock.Dispose();
#endif
}
