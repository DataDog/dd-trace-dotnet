// <copyright file="ReaderWriterLock.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETFRAMEWORK

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Concurrency;

internal partial class ReaderWriterLock : IDisposable
{
    private readonly System.Threading.ReaderWriterLock _readerWriterLock = new();

    internal bool IsReadLockHeld => _readerWriterLock.IsReaderLockHeld;

    internal bool EnterReadLock()
    {
        try
        {
            _readerWriterLock.AcquireReaderLock(TimeoutInMs);
            return true;
        }
        catch (ApplicationException)
        {
            Log.Error<int>("Couldn't acquire reader lock in {Timeout} ms", TimeoutInMs);
            return false;
        }
    }

    internal bool EnterWriteLock(int timeout = TimeoutInMs)
    {
        try
        {
            _readerWriterLock.AcquireWriterLock(timeout);
            return true;
        }
        catch (ApplicationException)
        {
            Log.Error<int>("Couldn't acquire writer lock in {Timeout} ms", timeout);
            return false;
        }
    }

    internal void ExitReadLock()
    {
        if (_readerWriterLock.IsReaderLockHeld)
        {
            _readerWriterLock.ReleaseReaderLock();
        }
        else
        {
            // this can happen, as Context can be created on a thread, disposed on another
            Log.Debug<int>("Read lock wasn't held in the timeout {Timeout}", TimeoutInMs);
        }
    }

    internal void ExitWriteLock()
    {
        if (_readerWriterLock.IsWriterLockHeld)
        {
            _readerWriterLock.ReleaseWriterLock();
        }
        else
        {
            Log.Information("Writer lock wasn't held");
        }
    }

    public void Dispose()
    {
    }
}

#endif
