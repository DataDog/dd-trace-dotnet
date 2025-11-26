#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.Util;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
public class CharSliceBenchmark
{
    [IterationSetup]
    public void Setup()
    {
        // Force GC to ensure clean state and reduce variance from GC timing
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Benchmark]
    public void OriginalCharSlice()
    {
        for (var i = 0; i < 10000; i++)
        {
            using var slice = new CharSliceV0("This is a test string for CharSliceV0");
            Consume(slice);
        }
    }
    
    [Benchmark]
    public void OptimizedCharSlice()
    {
        for (var i = 0; i < 10000; i++)
        {
            using var slice = new CharSliceV1("This is a test string for CharSliceV1");
            Consume(slice);
        }
    }
    
    [Benchmark]
    public void OptimizedCharSliceWithPool()
    {
        for (var i = 0; i < 10000; i++)
        {
            using var slice = new CharSliceV2("This is a test string for CharSliceV2");
            Consume(slice);
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Consume<T>(T slice) where T : IDisposable
    {
        _ = slice;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct CharSliceV0 : IDisposable
    {
        internal nint Ptr;
        internal nuint Len;

        internal CharSliceV0(string? str)
        {
            if (str == null)
            {
                Ptr = IntPtr.Zero;
                Len = UIntPtr.Zero;
            }
            else
            {
                // copy over str to unmanaged memory
                var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                Ptr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, Ptr, bytes.Length);
                Len = (nuint)bytes.Length;
            }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Ptr);
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct CharSliceV1 : IDisposable
    {
        internal readonly nint Ptr;
        internal readonly nuint Len;

        internal CharSliceV1(string? str)
        {
            if (str == null)
            {
                Ptr = IntPtr.Zero;
                Len = UIntPtr.Zero;
            }
            else
            {
                var encoding = Encoding.UTF8;
                var maxBytesCount = encoding.GetMaxByteCount(str.Length);
                Ptr = Marshal.AllocHGlobal(maxBytesCount);
                unsafe
                {
                    fixed (char* strPtr = str)
                    {
                        Len = (nuint)encoding.GetBytes(strPtr, str.Length, (byte*)Ptr, maxBytesCount);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Ptr == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(Ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct CharSliceV2 : IDisposable
    {
        private const int MaxBytesForMaxStringLength = (4096 * 2) + 1; // 4096 characters max, UTF-8 encoding can take up to 2 bytes per character, plus 1 for the null terminator
        private const int PoolSize = 25; // Number of segments to keep in the pool

        /// <summary>
        /// Memory pool for managing unmanaged memory allocations for <see cref="CharSliceV2"/>.
        /// </summary>
        [ThreadStatic]
        private static UnmanagedMemoryPool? _unmanagedPool;

        /// <summary>
        /// Pointer to the start of the slice.
        /// </summary>
        internal readonly nint Ptr;

        /// <summary>
        /// Length of the slice.
        /// </summary>
        internal readonly nuint Len;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharSlice"/> struct.
        /// This can be further optimized if we can avoid copying the string to unmanaged memory.
        /// </summary>
        /// <param name="str">The string to copy into memory.</param>
        internal CharSliceV2(string? str)
        {
            if (str == null)
            {
                Ptr = IntPtr.Zero;
                Len = UIntPtr.Zero;
            }
            else
            {
                var encoding = Encoding.UTF8;
                var maxBytesCount = encoding.GetMaxByteCount(str.Length);
                _unmanagedPool ??= new(MaxBytesForMaxStringLength, PoolSize);
                Ptr = _unmanagedPool.Rent();

                unsafe
                {
                    fixed (char* strPtr = str)
                    {
                        Len = (nuint)encoding.GetBytes(strPtr, str.Length, (byte*)Ptr, maxBytesCount);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Ptr == IntPtr.Zero)
            {
                return;
            }

            _unmanagedPool?.Return(Ptr);
        }
    }
}
