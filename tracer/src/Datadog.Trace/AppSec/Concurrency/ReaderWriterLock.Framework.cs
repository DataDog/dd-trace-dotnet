// <copyright file="ReaderWriterLock.Framework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec.Concurrency;

internal partial class ReaderWriterLock : IDisposable
{
    private const int Timeout = 4000;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ReaderWriterLock>();
#if NETFRAMEWORK
    private readonly System.Threading.ReaderWriterLock _readerWriterLock = new();

    internal bool IsReadLockHeld => _readerWriterLock.IsReaderLockHeld;

    internal bool EnterReadLock()
    {
        try
        {
            _readerWriterLock.AcquireReaderLock(Timeout);
            return true;
        }
        catch (ApplicationException)
        {
            Log.Error("Couldn't acquire reader lock in {timeout} ms", Timeout.ToString());
            return false;
        }
    }

    internal bool EnterWriteLock()
    {
        try
        {
            _readerWriterLock.AcquireWriterLock(Timeout);
            return true;
        }
        catch (ApplicationException)
        {
            Log.Error("Couldn't acquire writer lock in {timeout} ms", Timeout.ToString());
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
            Log.Debug("Read lock wasn't held", Timeout.ToString());
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
            Log.Information("Writer lock wasn't held", Timeout.ToString());
        }
    }

    public void Dispose()
    {
    }
#endif
}
