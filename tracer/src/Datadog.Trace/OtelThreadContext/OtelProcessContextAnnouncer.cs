// <copyright file="OtelProcessContextAnnouncer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Adds the <c>threadlocal.*</c> entries required by OTEP 4947 to the OTEP 4719 process context.
/// <para>
/// Readers refuse to look for the <c>otel_thread_ctx_v1</c> symbol until the process context advertises
/// <c>threadlocal.schema_version</c>, so without this the thread context records we publish are correct but
/// invisible.
/// </para>
/// <para>
/// The tracer already publishes a process context through libdatadog (see
/// <c>ServiceDiscoveryHelper.StoreTracerMetadata</c>), and the spec allows only one per process, so this
/// extends the existing one rather than publishing another. libdatadog keeps owning the mapping - which it
/// holds in a Rust static for the life of the process - and its payload buffer; all we do is point the
/// header at a longer payload of our own, following the OTEP 4719 update protocol.
/// </para>
/// <para>
/// See docs/OTelContextPropagation.md.
/// </para>
/// </summary>
internal static class OtelProcessContextAnnouncer
{
    // Header layout from OTEP 4719. All scalars are native endianness.
    //
    // offset size field
    //      0    8 signature                 "OTEL_CTX", not NUL-terminated
    //      8    4 version                   currently 2
    //     12    4 payload_size
    //     16    8 monotonic_published_at_ns 0 means "being mutated, do not read"
    //     24    8 payload                   pointer, may point anywhere in the process
    private const int SignatureOffset = 0;
    private const int VersionOffset = 8;
    private const int PayloadSizeOffset = 12;
    private const int TimestampOffset = 16;
    private const int PayloadOffset = 24;

    private const uint SupportedVersion = 2;

    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("OTEL_CTX");

    // The names /proc/<pid>/maps gives the mapping, depending on which of memfd_create and
    // prctl(PR_SET_VMA_ANON_NAME) the publisher managed to use.
    private static readonly string[] MappingNames =
    [
        "[anon_shmem:OTEL_CTX",
        "[anon:OTEL_CTX",
        "/memfd:OTEL_CTX",
    ];

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtelProcessContextAnnouncer));

    private static int _announced;

    /// <summary>
    /// Announces the thread context schema, once per process. Never throws: if anything is missing or
    /// unexpected the process context is left exactly as libdatadog published it.
    /// </summary>
    public static void Announce(TracerSettings settings)
    {
        if (!settings.OtelThreadContextEnabled || !OtelThreadContextPublisher.IsPlatformSupported())
        {
            return;
        }

        if (Interlocked.Exchange(ref _announced, 1) != 0)
        {
            return;
        }

        try
        {
            var payload = ThreadLocalMetadataPayload.Encode([ThreadLocalMetadataPayload.LocalRootSpanIdKey]);

            if (TryAnnounce(payload, out var failure))
            {
                Log.Information("Announced the OpenTelemetry thread context schema in the process context.");
            }
            else
            {
                Log.Information(
                    "Could not announce the OpenTelemetry thread context schema ({Reason}). Thread contexts are still published, but external readers will not discover them.",
                    failure);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to announce the OpenTelemetry thread context schema in the process context.");
        }
    }

    internal static bool TryAnnounce(byte[] extraAttributes, out string failure)
    {
        string maps;

        try
        {
            maps = File.ReadAllText("/proc/self/maps");
        }
        catch (Exception ex)
        {
            failure = $"/proc/self/maps could not be read: {ex.Message}";
            return false;
        }

        if (!TryParseMappingAddress(maps, out var header))
        {
            failure = "no OTEL_CTX mapping is published";
            return false;
        }

        return TryExtendPayload(header, extraAttributes, out failure);
    }

    /// <summary>
    /// Finds the start address of the process context mapping in the contents of <c>/proc/self/maps</c>.
    /// Lines look like <c>7f..000-7f..000 rw-p 00000000 00:01 12345 /memfd:OTEL_CTX (deleted)</c>.
    /// </summary>
    internal static bool TryParseMappingAddress(string maps, out IntPtr address)
    {
        foreach (var line in maps.Split('\n'))
        {
            if (!HasMappingName(line))
            {
                continue;
            }

            var separator = line.IndexOf('-');

            if (separator > 0 && TryParseHex(line, separator, out var start))
            {
                address = (IntPtr)start;
                return true;
            }
        }

        address = IntPtr.Zero;
        return false;
    }

    /// <summary>
    /// Parses the leading hexadecimal address of a /proc/self/maps line. Hand-rolled because the span
    /// overloads of <c>ulong.TryParse</c> are not available on every target framework, and the address
    /// field has no fixed width.
    /// </summary>
    private static bool TryParseHex(string line, int length, out ulong value)
    {
        value = 0;

        if (length is 0 or > 16)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            var digit = HexConverter.FromChar(line[i]);

            if (digit == 0xFF)
            {
                return false;
            }

            value = (value << 4) | (uint)digit;
        }

        return true;
    }

    private static bool HasMappingName(string line)
    {
        foreach (var name in MappingNames)
        {
            // the name is the last column, so a plain search is enough to tell the mapping apart
            if (line.IndexOf(name, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends <paramref name="extraAttributes"/> to the payload the header points at, and republishes
    /// following the OTEP 4719 update protocol.
    /// </summary>
    internal static unsafe bool TryExtendPayload(IntPtr headerAddress, byte[] extraAttributes, out string failure)
    {
        var header = (byte*)headerAddress;

        for (var i = 0; i < Signature.Length; i++)
        {
            if (header[SignatureOffset + i] != Signature[i])
            {
                failure = "the mapping does not carry the OTEL_CTX signature";
                return false;
            }
        }

        var version = *(uint*)(header + VersionOffset);

        if (version != SupportedVersion)
        {
            failure = "process context version " + version.ToString() + " is not supported";
            return false;
        }

        var timestamp = Volatile.Read(ref *(long*)(header + TimestampOffset));

        if (timestamp == 0)
        {
            // Someone else is mid-update. This runs once at startup, right after libdatadog published
            // synchronously, so rather than spin we leave the context alone.
            failure = "the process context is being updated by another writer";
            return false;
        }

        var payloadSize = *(uint*)(header + PayloadSizeOffset);
        var payload = *(byte**)(header + PayloadOffset);

        if (payload == null || payloadSize == 0)
        {
            failure = "the process context has no payload";
            return false;
        }

        var existing = new ReadOnlySpan<byte>(payload, (int)payloadSize);

        if (AlreadyAnnounced(existing))
        {
            failure = "the process context already advertises a thread context schema";
            return false;
        }

        // Build the extended payload in unmanaged memory. It is never freed: the header points at it for
        // the life of the process, and readers in other processes may be looking at it at any time.
        // libdatadog's own payload is left untouched and still owned by its Rust handle.
        var extendedSize = (int)payloadSize + extraAttributes.Length;
        var extended = (byte*)Marshal.AllocHGlobal(extendedSize);
        existing.CopyTo(new Span<byte>(extended, (int)payloadSize));
        extraAttributes.AsSpan().CopyTo(new Span<byte>(extended + payloadSize, extraAttributes.Length));

        // OTEP 4719 update protocol. Readers check the timestamp before and after reading the payload, so
        // zeroing it first and restoring it last is what makes a concurrent read either see the old payload
        // or retry, never a mix of the two.
        Volatile.Write(ref *(long*)(header + TimestampOffset), 0);
        Thread.MemoryBarrier();

        *(byte**)(header + PayloadOffset) = extended;
        *(uint*)(header + PayloadSizeOffset) = (uint)extendedSize;

        Thread.MemoryBarrier();
        Volatile.Write(ref *(long*)(header + TimestampOffset), NextTimestamp(timestamp));

        failure = string.Empty;
        return true;
    }

    /// <summary>
    /// Produces the new publication timestamp. The spec requires a value that is non-zero and strictly
    /// after the previous one; it recommends <c>CLOCK_BOOTTIME</c>, which managed code cannot read. A
    /// monotonic reading is used where it is already ahead, and otherwise the previous value is simply
    /// advanced - readers only use this field to detect change and torn reads.
    /// </summary>
    private static long NextTimestamp(long previous)
    {
        var monotonicNs = (long)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));
        return monotonicNs > previous ? monotonicNs : previous + 1;
    }

    private static bool AlreadyAnnounced(ReadOnlySpan<byte> payload)
    {
        // Guards against a future libdatadog that emits the threadlocal.* keys itself, which would
        // otherwise leave two copies of each key in the payload.
        var key = Encoding.UTF8.GetBytes(ThreadLocalMetadataPayload.SchemaVersionAttribute);

        for (var i = 0; i + key.Length <= payload.Length; i++)
        {
            if (payload.Slice(i, key.Length).SequenceEqual(key))
            {
                return true;
            }
        }

        return false;
    }
}
