// <copyright file="Reactor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
#if SHARED_REMOTE_CONFIG
using Datadog.Trace.SourceGenerators;
#endif

namespace Datadog.Trace.RemoteConfigurationManagement.Shared;

internal sealed class Reactor : IDisposable
{
    internal const int MaxBufferBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IReactorBinding _binding;
    private readonly Func<int, int> _allocate;
    private readonly Func<int, int> _length;
    private readonly Func<int, int> _free;
    private readonly Func<int, int, int, int, int> _reply;

    private Reactor(IReactorBinding binding)
    {
        _binding = binding;
        _allocate = binding.BindUnary("rc_alloc");
        _length = binding.BindUnary("rc_buffer_len");
        _free = binding.BindUnary("rc_free");
        _reply = binding.BindQuaternary("rc_http_succeed");
        Open = binding.BindUnary("rc_open");
        Retarget = binding.BindBinary("rc_retarget");
        SetIdentity = binding.BindBinary("rc_set_identity");
        SetInterest = binding.BindBinary("rc_set_interest");
        TimerElapsed = binding.BindBinary("rc_timer_elapsed");
        HttpFail = binding.BindTernary("rc_http_fail");
        Poll = binding.BindUnary("rc_poll");
        Event = binding.BindUnary("rc_event");
        Result = binding.BindUnary("rc_result");
        Snapshot = binding.BindUnary("rc_snapshot");
        Describe = binding.BindUnary("rc_view_describe");
        File = binding.BindTernary("rc_view_file");
        Release = binding.BindUnary("rc_view_release");
        Apply = binding.BindQuaternary("rc_apply");
        Cancel = binding.BindUnary("rc_cancel");
        Close = binding.BindUnary("rc_close");
    }

    internal Func<int, int> Open { get; }

    internal Func<int, int, int> Retarget { get; }

    internal Func<int, int, int> SetIdentity { get; }

    internal Func<int, int, int> SetInterest { get; }

    internal Func<int, int, int> TimerElapsed { get; }

    internal Func<int, int, int, int> HttpFail { get; }

    internal Func<int, int> Poll { get; }

    internal Func<int, int> Event { get; }

    internal Func<int, int> Result { get; }

    internal Func<int, int> Snapshot { get; }

    internal Func<int, int> Describe { get; }

    internal Func<int, int, int, int> File { get; }

    internal Func<int, int> Release { get; }

    internal Func<int, int, int, int, int> Apply { get; }

    internal Func<int, int> Cancel { get; }

#if SHARED_REMOTE_CONFIG
    [TestingAndPrivateOnly]
#endif
    internal Func<int, int> Close { get; }

    public void Dispose() => _binding.Dispose();

    internal static Reactor OpenNative(string path) => OpenBinding(new NativeReactorBinding(path));

#if SHARED_REMOTE_CONFIG
    [TestingAndPrivateOnly]
#endif
    internal static Reactor OpenBinding(IReactorBinding binding)
    {
        try
        {
            return new Reactor(binding);
        }
        catch
        {
            binding.Dispose();
            throw;
        }
    }

    internal static int[] GetCapabilityIndices(ReadOnlySpan<byte> bigEndianBits)
    {
        var indices = new List<int>();
        for (var offset = 0; offset < bigEndianBits.Length; offset++)
        {
            var value = bigEndianBits[bigEndianBits.Length - 1 - offset];
            for (var bit = 0; value != 0; bit++, value >>= 1)
            {
                if ((value & 1) != 0)
                {
                    indices.Add((offset * 8) + bit);
                }
            }
        }

        return indices.ToArray();
    }

    internal int Check(int result)
    {
        if (result != -1)
        {
            return result;
        }

        var error = _binding.GetError();
        throw new InvalidOperationException(error > 0 ? Encoding.UTF8.GetString(Take(error)) : "Missing reactor error");
    }

    internal int WithJson<TValue>(TValue value, Func<int, int> operation)
        => WithBytes(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), operation);

    internal void CompleteHttp(int client, int operation, ReadOnlySpan<byte> input, int statusCode = 200)
        => ReplyHttp(client, operation, Write(input), statusCode);

    internal void CompleteHttp(int client, int operation, List<ArraySegment<byte>> segments, int length, int statusCode)
    {
        var body = Check(_allocate(length));
        try
        {
            var destination = _binding.BorrowWrite(body, length);
            foreach (var segment in segments)
            {
                segment.AsSpan().CopyTo(destination);
                destination = destination[segment.Count..];
            }
        }
        catch
        {
            Free(body);
            throw;
        }

        ReplyHttp(client, operation, body, statusCode);
    }

    internal int WithText(string value, Func<int, int> operation)
    {
        return WithBytes(Encoding.UTF8.GetBytes(value), operation);
    }

    internal int WithBytes(ReadOnlySpan<byte> value, Func<int, int> operation)
    {
        var handle = Write(value);
        try
        {
            return Check(operation(handle));
        }
        finally
        {
            Free(handle);
        }
    }

    internal WatchOperations BindWatch() => new(_binding);

    internal JsonElement TakeJson(int handle) => TakeJson<JsonElement>(handle);

    internal TValue TakeJson<TValue>(int handle)
    {
        try
        {
            return JsonSerializer.Deserialize<TValue>(Read(handle), JsonOptions)!;
        }
        finally
        {
            Free(handle);
        }
    }

    internal byte[] Take(int handle)
    {
        try
        {
            return Copy(handle);
        }
        finally
        {
            Free(handle);
        }
    }

    internal ArraySegment<byte> TakePooled(int handle, ArrayPool<byte> pool)
    {
        byte[]? bytes = null;
        try
        {
            int length;
            try
            {
                var input = Read(handle);
                length = input.Length;
                bytes = pool.Rent(length);
                input.CopyTo(bytes);
            }
            finally
            {
                Free(handle);
            }

            return new ArraySegment<byte>(bytes, 0, length);
        }
        catch
        {
            if (bytes is not null)
            {
                pool.Return(bytes);
            }

            throw;
        }
    }

#if SHARED_REMOTE_CONFIG
    [TestingAndPrivateOnly]
#endif
    internal byte[] Copy(int handle) => Read(handle).ToArray();

    internal void Free(int handle) => Check(_free(handle));

    private void ReplyHttp(int client, int operation, int body, int statusCode)
    {
        var metadata = 0;
        var transferred = false;
        try
        {
            metadata = Write(statusCode == 200 ? "{\"status\":200}"u8 : JsonSerializer.SerializeToUtf8Bytes(new { status = statusCode }));
            // The reactor takes the body on success. Metadata stays with the caller.
            Check(_reply(client, operation, metadata, body));
            transferred = true;
        }
        finally
        {
            try
            {
                if (!transferred)
                {
                    Free(body);
                }
            }
            finally
            {
                if (metadata != 0)
                {
                    Free(metadata);
                }
            }
        }
    }

    private ReadOnlySpan<byte> Read(int handle)
    {
        var length = Check(_length(handle));
        if ((uint)length > MaxBufferBytes)
        {
            throw new InvalidOperationException("Invalid reactor buffer length");
        }

        return _binding.BorrowRead(handle, length);
    }

    private int Write(ReadOnlySpan<byte> bytes)
    {
        var handle = Check(_allocate(bytes.Length));
        try
        {
            bytes.CopyTo(_binding.BorrowWrite(handle, bytes.Length));
            return handle;
        }
        catch
        {
            Free(handle);
            throw;
        }
    }

    internal sealed class WatchOperations(IReactorBinding binding)
    {
        internal Func<int, int, int> Open { get; } = binding.BindBinary("rc_watch_open");

        internal Func<int, int, int> Close { get; } = binding.BindBinary("rc_watch_close");

        internal Func<int, int, int> Next { get; } = binding.BindBinary("rc_watch_next");

        internal Func<int, int> Snapshot { get; } = binding.BindUnary("rc_view_snapshot");

        internal Func<int, int, int, int, int> Finish { get; } = binding.BindQuaternary("rc_watch_finish");
    }
}
