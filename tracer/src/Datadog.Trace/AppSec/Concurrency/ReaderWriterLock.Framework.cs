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
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ReaderWriterLock>();
    private readonly System.Threading.ReaderWriterLock _readerWriterLock = new();

    internal bool TryEnterReadLock()
    {
        try
        {
            _readerWriterLock.AcquireReaderLock(Timeout);
            return true;
        }
        catch (ApplicationException)
        {
            Log.Error("ReaderWriterLock couldn't acquire lock in {timeout} ms", Timeout.ToString());
            return false;
        }
    }

    internal bool TryEnterWriteLock()
    {
        try
        {
            _readerWriterLock.AcquireWriterLock(Timeout);
            return true;
        }
        catch (ApplicationException)
        {
            Log.Error("ReaderWriterLock couldn't acquire lock in {timeout} ms", Timeout.ToString());
            return false;
        }
    }

    public void Dispose()
    {
    }
}
#endif
