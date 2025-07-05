// <copyright file="AsyncTextWriterSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;

namespace Datadog.Trace.Logging.Internal.Sinks;

/// <summary>
/// A buffered sink that writes log events to a <see cref="TextWriter"/> using a background thread.
/// </summary>
#if NETCOREAPP3_1_OR_GREATER
internal sealed class AsyncTextWriterSink : ILogEventSink, IDisposable, IAsyncDisposable
#else
internal sealed class AsyncTextWriterSink : ILogEventSink, IDisposable
#endif
{
    // in-memory buffer used only from the background thread
    private readonly StringBuilder _buffer;
    private readonly StringWriter _bufferWriter;

    private readonly ITextFormatter _textFormatter;
    private readonly BlockingCollection<LogEvent> _writeQueue;
    private readonly TextWriter _textWriter;
    private readonly Task _writeTask;

    public AsyncTextWriterSink(ITextFormatter formatter, TextWriter textWriter, int queueLimit)
    {
        // in-memory buffer used only from the background thread
        _buffer = new StringBuilder(capacity: 4096);
        _bufferWriter = new StringWriter(_buffer);

        _textFormatter = formatter;
        _textWriter = textWriter;
        _writeQueue = new BlockingCollection<LogEvent>(queueLimit);

        _writeTask = Task.Factory.StartNew(
            WriteBufferedLogEvents,
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    public void Emit(LogEvent? logEvent)
    {
        if (logEvent is null || _writeQueue.IsAddingCompleted || _writeTask.IsCompleted)
        {
            // skip null log events.
            // if we are no longer writing to the console, don't accept any more log events.
            return;
        }

        try
        {
            // try to add the log event to the queue (non-blocking).
            // if the queue is full, the log event is dropped.
            _writeQueue.TryAdd(logEvent);
        }
        catch (InvalidOperationException)
        {
            // queue is marked as complete, drop log event.
            // this won't usually happen as we check IsAddingCompleted above,
            // but it can theoretically happen if the queue is completed from another thread
            // after we checked IsAddingCompleted but before we called TryAdd().
        }
    }

    private void WriteBufferedLogEvents()
    {
        try
        {
            // GetConsumingEnumerable() blocks until an item
            // is available or the collection is marked as complete.
            // The foreach loop will exit when the collection is marked as complete.
            foreach (var logEvent in _writeQueue.GetConsumingEnumerable())
            {
                // clear in-memory buffer and format event into buffer
                _buffer.Clear();
                _textFormatter.Format(logEvent, _bufferWriter);

                // We intentionally pass the StringBuilder directly to the TextWriter:
                // - In newer runtimes, this calls the new TextWriter.Write(StringBuilder) overload,
                //   which uses StringBuilder.GetChunks() internally without allocating a new string.
                // - In older runtimes, this calls the TextWriter.Write(object) overload,
                //   which calls StringBuilder.ToString() internally.
                _textWriter.Write(_buffer);
            }
        }
        catch (Exception)
        {
            // Nowhere to log safely so just swallow!
        }
    }

    public async Task FlushAsync()
    {
        _writeQueue.CompleteAdding();
        await _writeTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        FlushAsync().SafeWait();
        _bufferWriter.Dispose();
        _textWriter.Dispose();
        _writeQueue.Dispose();
    }

#if NETCOREAPP3_1_OR_GREATER
    public async ValueTask DisposeAsync()
    {
        await FlushAsync().ConfigureAwait(false);
        await _bufferWriter.DisposeAsync().ConfigureAwait(false);
        await _textWriter.DisposeAsync().ConfigureAwait(false);
        _writeQueue.Dispose();
    }
#endif
}
