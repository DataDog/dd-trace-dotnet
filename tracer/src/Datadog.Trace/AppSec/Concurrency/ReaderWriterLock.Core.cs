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

    internal bool EnterReadLock()
    {
        if (!_readerWriterLock.TryEnterReadLock(Timeout))
        {
            Log.Error("Couldn't acquire reader lock in {timeout} ms", Timeout.ToString());
            return false;
        }

        return true;
    }

    internal bool EnterWriteLock()
    {
        if (!_readerWriterLock.TryEnterWriteLock(Timeout))
        {
            Log.Error("Couldn't acquire writer lock in {timeout} ms", Timeout.ToString());
            return false;
        }

        return true;
    }

    internal void ExitReadLock()
    {
        if (_readerWriterLock.IsReadLockHeld)
        {
            _readerWriterLock.ExitReadLock();
        }
        else
        {
            Log.Warning("Reader lock wasn't held", Timeout.ToString());
        }
    }

    internal void ExitWriteLock()
    {
        if (_readerWriterLock.IsWriteLockHeld)
        {
            _readerWriterLock.ExitWriteLock();
        }
        else
        {
            Log.Warning("Writer lock wasn't held", Timeout.ToString());
        }
    }

    public void Dispose() => _readerWriterLock.Dispose();
}
#endif
