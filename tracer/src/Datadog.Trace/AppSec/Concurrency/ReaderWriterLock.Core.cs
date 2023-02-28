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

    internal bool IsReadLockHeld => _readerWriterLock.IsReadLockHeld;

    internal bool EnterReadLock()
    {
        if (!_readerWriterLock.TryEnterReadLock(TimeoutInMs))
        {
            Log.Error<int>("Couldn't acquire reader lock in {Timeout} ms", TimeoutInMs);
            return false;
        }

        return true;
    }

    internal bool EnterWriteLock()
    {
        if (!_readerWriterLock.TryEnterWriteLock(TimeoutInMs))
        {
            Log.Error<int>("Couldn't acquire writer lock in {Timeout} ms", TimeoutInMs);
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
            // this can happen, as Context can be created on a thread, disposed on another
            Log.Debug<int>("Reader lock wasn't held {Timeout}", TimeoutInMs);
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
            Log.Information("Writer lock wasn't held");
        }
    }

    public void Dispose() => _readerWriterLock.Dispose();
}
#endif
