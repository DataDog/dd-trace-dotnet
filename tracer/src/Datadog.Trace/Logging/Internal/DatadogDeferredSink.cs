// <copyright file="DatadogDeferredSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging;

#if !NETCOREAPP
internal sealed class DatadogDeferredSink : ILogEventSink, IFlushableFileSink, IDisposable
#else
internal sealed class DatadogDeferredSink : ILogEventSink, IFlushableFileSink, IDisposable, IThreadPoolWorkItem
#endif
{
    private readonly ConcurrentQueue<LogEvent> _queue;
    private readonly ILogEventSink _sink;
#if !NETCOREAPP
    private readonly WaitCallback _waitCallback;
#endif
    private long _active;

    public DatadogDeferredSink(ILogEventSink sink)
    {
        _sink = sink;
        _queue = new ConcurrentQueue<LogEvent>();
#if !NETCOREAPP
        _waitCallback = InternalWaitCallBackDelegate;
#endif
        _active = 0;
    }

    public void Emit(LogEvent logEvent)
    {
        _queue.Enqueue(logEvent);
        if (Interlocked.CompareExchange(ref _active, 1, 0) == 0)
        {
#if !NETCOREAPP
            ThreadPool.UnsafeQueueUserWorkItem(_waitCallback, null);
#else
            ThreadPool.UnsafeQueueUserWorkItem(this, true);
#endif
        }
    }

    public void FlushToDisk()
    {
#if !NETCOREAPP
        InternalWaitCallBackDelegate(null);
#else
        ((IThreadPoolWorkItem)this).Execute();
#endif
        if (_sink is IFlushableFileSink flushableFileSink)
        {
            flushableFileSink.FlushToDisk();
        }
    }

    public void Dispose()
    {
#if !NETCOREAPP
        InternalWaitCallBackDelegate(null);
#else
        ((IThreadPoolWorkItem)this).Execute();
#endif
        if (_sink is IDisposable disposableSink)
        {
            disposableSink.Dispose();
        }
    }

#if !NETCOREAPP
    private void InternalWaitCallBackDelegate(object state)
#else
    void IThreadPoolWorkItem.Execute()
#endif
    {
        try
        {
            while (_queue.TryDequeue(out var logItem))
            {
                _sink.Emit(logItem);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _active, 0);
        }
    }
}
