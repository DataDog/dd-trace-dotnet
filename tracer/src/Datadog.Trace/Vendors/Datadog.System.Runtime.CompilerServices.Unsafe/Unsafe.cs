﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Datadog.System.Runtime.Versioning;
using InlineIL;
using static InlineIL.IL.Emit;

// System.Runtime.CompilerServices.Unsafe does not have nullable reference type annotations
#nullable disable

namespace Datadog.System.Runtime.CompilerServices.Unsafe
{
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "UnusedParameter.Global")]
    [SuppressMessage("ReSharper", "EntityNameCapturedOnly.Global")]
    public static unsafe class Unsafe
    {
        // This is the InlineIL equivalent of System.Runtime.CompilerServices.Unsafe
        // https://github.com/dotnet/runtime/blob/release/6.0/src/libraries/System.Runtime.CompilerServices.Unsafe/src/System.Runtime.CompilerServices.Unsafe.il
        // Last update: 98ace7d4837fcd81c1f040b1f67e63e9e1973e13 - these methods became intrinsics starting from .NET 7

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(void* source)
        {
            Ldarg(nameof(source));
            Ldobj(typeof(T));
            return IL.Return<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadUnaligned<T>(void* source)
        {
            Ldarg(nameof(source));
            Unaligned(1);
            Ldobj(typeof(T));
            return IL.Return<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadUnaligned<T>(ref byte source)
        {
            Ldarg(nameof(source));
            Unaligned(1);
            Ldobj(typeof(T));
            return IL.Return<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(void* destination, T value)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(value));
            Stobj(typeof(T));
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUnaligned<T>(void* destination, T value)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(value));
            Unaligned(1);
            Stobj(typeof(T));
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUnaligned<T>(ref byte destination, T value)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(value));
            Unaligned(1);
            Stobj(typeof(T));
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(void* destination, ref T source)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldobj(typeof(T));
            Stobj(typeof(T));
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ref T destination, void* source)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldobj(typeof(T));
            Stobj(typeof(T));
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* AsPointer<T>(ref T value)
        {
            Ldarg(nameof(value));
            Conv_U();
            return IL.ReturnPointer();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipInit<T>(out T value)
        {
            Ret();
            throw IL.Unreachable();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>()
        {
            Sizeof(typeof(T));
            return IL.Return<int>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyBlock(void* destination, void* source, uint byteCount)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(byteCount));
            Cpblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyBlock(ref byte destination, ref byte source, uint byteCount)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(byteCount));
            Cpblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyBlockUnaligned(void* destination, void* source, uint byteCount)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(byteCount));
            Unaligned(1);
            Cpblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(byteCount));
            Unaligned(1);
            Cpblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitBlock(void* startAddress, byte value, uint byteCount)
        {
            Ldarg(nameof(startAddress));
            Ldarg(nameof(value));
            Ldarg(nameof(byteCount));
            Initblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitBlock(ref byte startAddress, byte value, uint byteCount)
        {
            Ldarg(nameof(startAddress));
            Ldarg(nameof(value));
            Ldarg(nameof(byteCount));
            Initblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitBlockUnaligned(void* startAddress, byte value, uint byteCount)
        {
            Ldarg(nameof(startAddress));
            Ldarg(nameof(value));
            Ldarg(nameof(byteCount));
            Unaligned(1);
            Initblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount)
        {
            Ldarg(nameof(startAddress));
            Ldarg(nameof(value));
            Ldarg(nameof(byteCount));
            Unaligned(1);
            Initblk();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T As<T>(object o)
            where T : class
        {
            Ldarg(nameof(o));
            return IL.Return<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsRef<T>(void* source)
        {
            // For .NET Core the roundtrip via a local is no longer needed
#if NETCOREAPP
            IL.Push(source);
            return ref IL.ReturnRef<T>();
#else
            // Roundtrip via a local to avoid type mismatch on return that the JIT inliner chokes on.
            IL.DeclareLocals(
                false,
                new LocalVar("local", typeof(int).MakeByRefType())
            );

            IL.Push(source);
            Stloc("local");
            Ldloc("local");
            return ref IL.ReturnRef<T>();
#endif
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsRef<T>(in T source)
        {
            Ldarg(nameof(source));
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TTo As<TFrom, TTo>(ref TFrom source)
        {
            Ldarg(nameof(source));
            return ref IL.ReturnRef<TTo>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Unbox<T>(object box)
            where T : struct
        {
            IL.Push(box);
            IL.Emit.Unbox(typeof(T));
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Add<T>(ref T source, int elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Conv_I();
            Mul();
            IL.Emit.Add();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* Add<T>(void* source, int elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Conv_I();
            Mul();
            IL.Emit.Add();
            return IL.ReturnPointer();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Add<T>(ref T source, IntPtr elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Mul();
            IL.Emit.Add();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Add<T>(ref T source, nuint elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Mul();
            IL.Emit.Add();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AddByteOffset<T>(ref T source, IntPtr byteOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(byteOffset));
            IL.Emit.Add();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AddByteOffset<T>(ref T source, nuint byteOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(byteOffset));
            IL.Emit.Add();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Subtract<T>(ref T source, int elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Conv_I();
            Mul();
            Sub();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* Subtract<T>(void* source, int elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Conv_I();
            Mul();
            Sub();
            return IL.ReturnPointer();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Subtract<T>(ref T source, IntPtr elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Mul();
            Sub();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Subtract<T>(ref T source, nuint elementOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(elementOffset));
            Sizeof(typeof(T));
            Mul();
            Sub();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T SubtractByteOffset<T>(ref T source, IntPtr byteOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(byteOffset));
            Sub();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T SubtractByteOffset<T>(ref T source, nuint byteOffset)
        {
            Ldarg(nameof(source));
            Ldarg(nameof(byteOffset));
            Sub();
            return ref IL.ReturnRef<T>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr ByteOffset<T>(ref T origin, ref T target)
        {
            Ldarg(nameof(target));
            Ldarg(nameof(origin));
            Sub();
            return IL.Return<IntPtr>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSame<T>(ref T left, ref T right)
        {
            Ldarg(nameof(left));
            Ldarg(nameof(right));
            Ceq();
            return IL.Return<bool>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressGreaterThan<T>(ref T left, ref T right)
        {
            Ldarg(nameof(left));
            Ldarg(nameof(right));
            Cgt_Un();
            return IL.Return<bool>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressLessThan<T>(ref T left, ref T right)
        {
            Ldarg(nameof(left));
            Ldarg(nameof(right));
            Clt_Un();
            return IL.Return<bool>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullRef<T>(ref T source)
        {
            Ldarg(nameof(source));
            Ldc_I4_0();
            Conv_U();
            Ceq();
            return IL.Return<bool>();
        }

        [NonVersionable]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T NullRef<T>()
        {
            Ldc_I4_0();
            Conv_U();
            return ref IL.ReturnRef<T>();
        }
    }
}

namespace Datadog.System.Runtime.Versioning
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class NonVersionableAttribute : Attribute
    {
    }
}























//using Datadog.System.Runtime.Versioning;

//namespace Datadog.System.Runtime.CompilerServices.Unsafe
//{
//      public static partial class Unsafe
//    {
//        public static ref T AddByteOffset<T>(ref T source, System.IntPtr byteOffset) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public static ref T AddByteOffset<T>(ref T source, nuint byteOffset) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void* Add<T>(void* source, int elementOffset) { throw null; }
//        public static ref T Add<T>(ref T source, int elementOffset) { throw null; }
//        public static ref T Add<T>(ref T source, System.IntPtr elementOffset) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public static ref T Add<T>(ref T source, nuint elementOffset) { throw null; }
//        public static bool AreSame<T>([System.Diagnostics.CodeAnalysis.AllowNull] ref T left, [System.Diagnostics.CodeAnalysis.AllowNull] ref T right) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void* AsPointer<T>(ref T value) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static ref T AsRef<T>(void* source) { throw null; }
//        public static ref T AsRef<T>(scoped in T source) { throw null; }
//        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute("o")]
//        public static T? As<T>(object? o) where T : class? { throw null; }
//        public static ref TTo As<TFrom, TTo>(ref TFrom source) { throw null; }
//        public static TTo BitCast<TFrom, TTo>(TFrom source) where TFrom : struct where TTo : struct { throw null; }
//        public static System.IntPtr ByteOffset<T>([System.Diagnostics.CodeAnalysis.AllowNull] ref T origin, [System.Diagnostics.CodeAnalysis.AllowNull] ref T target) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public static void CopyBlock(ref byte destination, ref byte source, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void CopyBlock(void* destination, void* source, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public static void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void CopyBlockUnaligned(void* destination, void* source, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void Copy<T>(void* destination, ref T source) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void Copy<T>(ref T destination, void* source) { }
//        [System.CLSCompliantAttribute(false)]
//        public static void InitBlock(ref byte startAddress, byte value, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void InitBlock(void* startAddress, byte value, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void InitBlockUnaligned(void* startAddress, byte value, uint byteCount) { }
//        public static bool IsAddressGreaterThan<T>([System.Diagnostics.CodeAnalysis.AllowNull] ref T left, [System.Diagnostics.CodeAnalysis.AllowNull] ref T right) { throw null; }
//        public static bool IsAddressLessThan<T>([System.Diagnostics.CodeAnalysis.AllowNull] ref T left, [System.Diagnostics.CodeAnalysis.AllowNull] ref T right) { throw null; }
//        public static bool IsNullRef<T>(ref T source) { throw null; }
//        public static ref T NullRef<T>() { throw null; }
//        public static T ReadUnaligned<T>(ref byte source) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static T ReadUnaligned<T>(void* source) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static T Read<T>(void* source) { throw null; }
//        public static void SkipInit<T>(out T value) { throw null; }
//        public static int SizeOf<T>() { throw null; }
//        public static ref T SubtractByteOffset<T>(ref T source, System.IntPtr byteOffset) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public static ref T SubtractByteOffset<T>(ref T source, nuint byteOffset) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void* Subtract<T>(void* source, int elementOffset) { throw null; }
//        public static ref T Subtract<T>(ref T source, int elementOffset) { throw null; }
//        public static ref T Subtract<T>(ref T source, System.IntPtr elementOffset) { throw null; }
//        [System.CLSCompliantAttribute(false)]
//        public static ref T Subtract<T>(ref T source, nuint elementOffset) { throw null; }
//        public static ref T Unbox<T>(object box) where T : struct { throw null; }
//        public static void WriteUnaligned<T>(ref byte destination, T value) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void WriteUnaligned<T>(void* destination, T value) { }
//        [System.CLSCompliantAttribute(false)]
//        public unsafe static void Write<T>(void* destination, T value) { }
//    }
//}
