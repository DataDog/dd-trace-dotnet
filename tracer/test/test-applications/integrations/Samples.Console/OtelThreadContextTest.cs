#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Console_
{
    internal static class OtelThreadContextTest
    {
        private const int TraceIdOffset = 0;
        private const int TraceIdSize = 16;
        private const int SpanIdOffset = 16;
        private const int SpanIdSize = 8;
        private const int ValidOffset = 24;
        private const int AttributesSizeOffset = 26;
        private const int AttributesOffset = 28;

#if NETCOREAPP3_0_OR_GREATER
        private static IntPtr _libdatadogHandle;
#endif

        public static async Task RunAsync()
        {
#if NETCOREAPP3_0_OR_GREATER
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException("The OpenTelemetry thread context test requires a 64-bit Linux process.");
            }

            IntPtr rootRecord;
            using (var rootScope = SampleHelpers.CreateScope("otel.thread-context.root"))
            {
                var expectedRoot = ExpectedContext.FromScope(rootScope);
                rootRecord = AssertCurrentContext("root", expectedRoot);

                using (var nestedScope = SampleHelpers.CreateScope("otel.thread-context.nested"))
                {
                    var expectedNested = ExpectedContext.FromScope(nestedScope);
                    var nestedRecord = AssertCurrentContext("nested", expectedNested);
                    AssertEqual(rootRecord, nestedRecord, "Nested scope replaced the thread context record instead of updating it in place.");
                }

                var restoredRecord = AssertCurrentContext("parent-restored", expectedRoot);
                AssertEqual(rootRecord, restoredRecord, "Restoring the parent scope changed the thread context record.");

                await Task.Run(() => AssertCurrentContext("async-transition", expectedRoot));
                AssertCurrentContext("async-restored", expectedRoot);
            }

            AssertCleared("cleared", rootRecord);

            using (var reusedScope = SampleHelpers.CreateScope("otel.thread-context.reused"))
            {
                var expectedReused = ExpectedContext.FromScope(reusedScope);
                var reusedRecord = AssertCurrentContext("reused", expectedReused);
                AssertEqual(rootRecord, reusedRecord, "Opening another scope after reset allocated a new thread context record.");
            }

            AssertCleared("cleared-again", rootRecord);
            System.Console.WriteLine("OTEL_THREAD_CONTEXT_TEST_OK");
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("The OpenTelemetry thread context test requires .NET Core 3.0 or later.");
#endif
        }

#if NETCOREAPP3_0_OR_GREATER
        private static IntPtr AssertCurrentContext(string scenario, ExpectedContext expected)
        {
            var record = GetCurrentRecord();
            if (record == IntPtr.Zero)
            {
                throw new InvalidOperationException($"{scenario}: otel_thread_ctx_v1 did not point to a context record.");
            }

            AssertEqual((byte)1, Marshal.ReadByte(record, ValidOffset), $"{scenario}: the context record was not valid.");
            AssertEqual(expected.TraceId, ReadBytes(record, TraceIdOffset, TraceIdSize), $"{scenario}: trace id mismatch.");
            AssertEqual(expected.SpanId, ReadBytes(record, SpanIdOffset, SpanIdSize), $"{scenario}: span id mismatch.");

            var attributesSize = (ushort)Marshal.ReadInt16(record, AttributesSizeOffset);
            AssertEqual((ushort)18, attributesSize, $"{scenario}: unexpected attribute payload size.");
            AssertEqual((byte)0, Marshal.ReadByte(record, AttributesOffset), $"{scenario}: local-root attribute key index mismatch.");
            AssertEqual((byte)16, Marshal.ReadByte(record, AttributesOffset + 1), $"{scenario}: local-root attribute length mismatch.");
            AssertEqual(expected.LocalRootSpanIdHex, ReadAscii(record, AttributesOffset + 2, 16), $"{scenario}: local-root span id mismatch.");

            System.Console.WriteLine($"OTEL_THREAD_CONTEXT_{scenario.ToUpperInvariant()}_OK");
            return record;
        }

        private static void AssertCleared(string scenario, IntPtr expectedRecord)
        {
            var record = GetCurrentRecord();
            if (record == IntPtr.Zero)
            {
                throw new InvalidOperationException($"{scenario}: the OpenTelemetry thread context was detached instead of cleared in place.");
            }

            AssertEqual(expectedRecord, record, $"{scenario}: clearing the OpenTelemetry thread context changed the record.");
            AssertEqual((byte)1, Marshal.ReadByte(record, ValidOffset), $"{scenario}: the cleared context record was not valid.");
            AssertEqual(new byte[TraceIdSize], ReadBytes(record, TraceIdOffset, TraceIdSize), $"{scenario}: trace id was not cleared.");
            AssertEqual(new byte[SpanIdSize], ReadBytes(record, SpanIdOffset, SpanIdSize), $"{scenario}: span id was not cleared.");

            var attributesSize = (ushort)Marshal.ReadInt16(record, AttributesSizeOffset);
            AssertEqual((ushort)18, attributesSize, $"{scenario}: unexpected cleared attribute payload size.");
            AssertEqual((byte)0, Marshal.ReadByte(record, AttributesOffset), $"{scenario}: local-root attribute key index mismatch.");
            AssertEqual((byte)16, Marshal.ReadByte(record, AttributesOffset + 1), $"{scenario}: local-root attribute length mismatch.");
            AssertEqual("0000000000000000", ReadAscii(record, AttributesOffset + 2, 16), $"{scenario}: local-root span id was not cleared.");

            System.Console.WriteLine($"OTEL_THREAD_CONTEXT_{scenario.ToUpperInvariant()}_OK");
        }

        private static IntPtr GetCurrentRecord()
        {
            if (_libdatadogHandle == IntPtr.Zero)
            {
                var profilerPath = Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH_64")
                                ?? Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH")
                                ?? Environment.GetEnvironmentVariable("COR_PROFILER_PATH");
                if (string.IsNullOrEmpty(profilerPath))
                {
                    throw new InvalidOperationException("Could not locate the native tracer library.");
                }

                var profilerDirectory = Path.GetDirectoryName(profilerPath)
                                     ?? throw new InvalidOperationException($"Could not determine the native tracer directory from '{profilerPath}'.");
                _libdatadogHandle = NativeLibrary.Load(Path.Combine(profilerDirectory, "libdatadog_profiling.so"));
            }

            var tlsSlot = NativeLibrary.GetExport(_libdatadogHandle, "otel_thread_ctx_v1");
            return Marshal.ReadIntPtr(tlsSlot);
        }

        private static byte[] ReadBytes(IntPtr address, int offset, int count)
        {
            var bytes = new byte[count];
            Marshal.Copy(IntPtr.Add(address, offset), bytes, 0, count);
            return bytes;
        }

        private static string ReadAscii(IntPtr address, int offset, int count)
        {
            return Encoding.ASCII.GetString(ReadBytes(address, offset, count));
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
            }
        }

        private static void AssertEqual(byte[] expected, byte[] actual, string message)
        {
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidOperationException($"{message} Expected '{ToHex(expected)}', got '{ToHex(actual)}'.");
            }
        }

        private static string ToHex(byte[] value)
        {
            var result = new StringBuilder(value.Length * 2);
            foreach (var item in value)
            {
                result.Append(item.ToString("x2"));
            }

            return result.ToString();
        }

        private readonly struct ExpectedContext
        {
            private ExpectedContext(byte[] traceId, byte[] spanId, string localRootSpanIdHex)
            {
                TraceId = traceId;
                SpanId = spanId;
                LocalRootSpanIdHex = localRootSpanIdHex;
            }

            public byte[] TraceId { get; }

            public byte[] SpanId { get; }

            public string LocalRootSpanIdHex { get; }

            public static ExpectedContext FromScope(IDisposable scope)
            {
                const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var span = scope.GetType().GetProperty("Span", Flags)?.GetValue(scope)
                        ?? throw new InvalidOperationException("Could not read the active Datadog span.");
                var spanType = span.GetType();
                var traceId = spanType.GetProperty("TraceId128", Flags)?.GetValue(span)
                           ?? throw new InvalidOperationException("Could not read the active Datadog trace id.");
                var traceIdType = traceId.GetType();
                var upper = (ulong)(traceIdType.GetField("Upper", Flags)?.GetValue(traceId)
                                 ?? throw new InvalidOperationException("Could not read the upper trace id."));
                var lower = (ulong)(traceIdType.GetField("Lower", Flags)?.GetValue(traceId)
                                 ?? throw new InvalidOperationException("Could not read the lower trace id."));
                var spanId = (ulong)(spanType.GetProperty("SpanId", Flags)?.GetValue(span)
                                  ?? throw new InvalidOperationException("Could not read the span id."));
                var localRootSpanId = (ulong)(spanType.GetProperty("RootSpanId", Flags)?.GetValue(span)
                                           ?? throw new InvalidOperationException("Could not read the local-root span id."));

                var traceIdBytes = new byte[TraceIdSize];
                WriteUInt64BigEndian(traceIdBytes, 0, upper);
                WriteUInt64BigEndian(traceIdBytes, SpanIdSize, lower);
                var spanIdBytes = new byte[SpanIdSize];
                WriteUInt64BigEndian(spanIdBytes, 0, spanId);
                var localRootSpanIdBytes = new byte[SpanIdSize];
                WriteUInt64BigEndian(localRootSpanIdBytes, 0, localRootSpanId);
                return new ExpectedContext(traceIdBytes, spanIdBytes, ToHex(localRootSpanIdBytes));
            }

            private static void WriteUInt64BigEndian(byte[] destination, int offset, ulong value)
            {
                for (var i = 0; i < sizeof(ulong); i++)
                {
                    destination[offset + i] = (byte)(value >> ((sizeof(ulong) - i - 1) * 8));
                }
            }
        }
#endif
    }
}
