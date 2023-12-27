// <copyright file="ConsoleSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting.Display;

/// <summary>
/// Loosely based on https://github.com/manigandham/serilog-sinks-fastconsole
/// </summary>
internal class ConsoleSink : ILogEventSink, IDisposable
{
    private readonly StringWriter _bufferWriter = new();
    private readonly TextWriter _consoleWriter;
    private readonly BoundedConcurrentQueue<LogEvent?> _writeQueue;
    private readonly Task _writeTask;

    private readonly MessageTemplateTextFormatter _textFormatter;
    private readonly ManualResetEventSlim _serializationMutex = new(initialState: false);
    private readonly TaskCompletionSource<bool> _processExit = new();

    public ConsoleSink(string messageTemplate, int queueLimit, TextWriter? consoleWriter = null)
    {
        _consoleWriter = consoleWriter ?? new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true };
        _textFormatter = new MessageTemplateTextFormatter(messageTemplate);
        _writeQueue = new BoundedConcurrentQueue<LogEvent?>(queueLimit);
        _writeTask = Task.Run(WriteToConsoleStream);
    }

    // logs are immediately queued to channel
    public void Emit(LogEvent? logEvent)
    {
        // TODO: metrics about dropped logs?
        if (_processExit.Task.IsCompleted)
        {
            return;
        }

        if (_writeQueue.TryEnqueue(logEvent))
        {
            if (!_serializationMutex.IsSet)
            {
                _serializationMutex.Set();
            }
        }
    }

    private async Task WriteToConsoleStream()
    {
        // cache reference to stringbuilder inside writer
        var sb = _bufferWriter.GetStringBuilder();
        var isFinalFlush = false;

        while (true)
        {
            try
            {
                while (_writeQueue.TryDequeue(out var logEvent))
                {
                    // console output is an IO stream
                    // format and write event to in-memory buffer and then flush to console async
                    // do not use the locking textwriter from console.out used by console.writeline
                    _textFormatter.Format(logEvent, _bufferWriter);

#if NET5_0_OR_GREATER
                    // use StringBuilder internal buffers directly without allocating a new string
                    foreach (var chunk in sb.GetChunks())
                    {
                        await _consoleWriter.WriteAsync(chunk).ConfigureAwait(false);
                    }
#else
                    // fallback to creating string output
                    await _consoleWriter.WriteAsync(sb.ToString()).ConfigureAwait(false);
#endif

                    sb.Clear();
                }
            }
            catch (Exception)
            {
                // Nowhere to log safely so just swallow!
            }

            await _consoleWriter.FlushAsync().ConfigureAwait(false);

            if (_processExit.Task.IsCompleted)
            {
                if (isFinalFlush)
                {
                    return;
                }

                // do one more loop to make sure everything is flushed
                if (!_serializationMutex.IsSet)
                {
                    _serializationMutex.Set();
                }

                isFinalFlush = true;
                continue;
            }

            _serializationMutex.Wait();
            _serializationMutex.Reset();
        }
    }

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (!_processExit.TrySetResult(true))
        {
            return;
        }

        if (disposing)
        {
            // Close write queue and wait until items are drained
            // then wait for all console output to be flushed.
            _writeTask.GetAwaiter().GetResult();

            _bufferWriter.Dispose();
            _consoleWriter.Dispose();
        }
    }
}
