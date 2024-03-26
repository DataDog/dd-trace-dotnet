// <copyright file="MethodsSignature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Samples.Computer01
{
    public class MethodsSignature : ScenarioBase
    {
        public override void OnProcess()
        {
            // The easiest way to test frames with sampling is to throw different exceptions
            // because at least one of each type will be sampled.
            // Allocations beyond 100 KB would be fine except that we could not test
            // the .NET Framework runtime
            TriggerExceptions();
        }

        // call methods with different signatures
        // Note: return type is not meaningful
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void TriggerExceptions()
        {
            ThrowVoid();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowVoid()
        {
            ThrowObject("this is the end");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowObject(object val)
        {
            ThrowBool(true);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowBool(bool bValue)
        {
            ThrowNumbers(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowNumbers(byte b, sbyte sb, Int16 i16, UInt16 ui16, Int32 i32, UInt32 ui32, Int64 i64, UInt64 ui64, float s, double d)
        {
            ThrowStringAndChar("this is the end...", '.');
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowStringAndChar(string v, char c)
        {
            ThrowNative(IntPtr.Zero, UIntPtr.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowNative(IntPtr ptr, UIntPtr uptr)
        {
            var matrix2 = new int[2, 2, 2];
            matrix2[0, 0, 0] = 0;
            matrix2[0, 0, 1] = 0;
            matrix2[0, 1, 0] = 1;
            matrix2[0, 1, 1] = 1;
            matrix2[1, 0, 0] = 2;
            matrix2[1, 0, 1] = 2;
            matrix2[1, 1, 0] = 3;
            matrix2[1, 1, 1] = 3;
            byte[][] jagged = new byte[3][];
            jagged[0] = new byte[] { 1, 3, 5, 7, 9 };
            jagged[1] = new byte[] { 0, 2, 4, 6 };
            jagged[2] = new byte[] { 11, 22 };

            ThrowArrays(
                new string[] { "this", "is", "the", "end" },
                matrix2,
                jagged);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowArrays(string[] a1, int[,,] matrix2, byte[][] jaggedArray)
        {
            MyStruct ms;
            ms.Member = 42;
            ThrowStruct(ms);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowStruct(MyStruct ms)
        {
            var mc = new MyClass();
            mc.Member = "this is the end...";
            ThrowClass(mc);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowClass(MyClass mc)
        {
            MyStruct ms;
            ms.Member = 42;
            ThrowWithRefs(ref mc, ref ms);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowWithRefs(ref MyClass mc, ref MyStruct ms)
        {
            ThrowGenericMethod1(true);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowGenericMethod1<T>(T element)
        {
            if (element is bool)
            {
                ThrowGenericMethod1(new MyClass());
            }
            else
            if (element is MyClass)
            {
                MyStruct ms;
                ms.Member = 42;
                ThrowGenericMethod1(ms);
            }
            else
            if (element is MyStruct)
            {
                ThrowGenericMethod1("this");
            }
            else
            if (element is string)
            {
                ThrowGenericMethod2<T, int, string, List<bool>>(element, 42, 666, "is", new List<bool> { true }, new List<int> { 42 });
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        // TKey1 = string
        // TValue = int
        // TKey2 = string
        // TKey3 = List<bool>
        // TKey4 = List<TValue>
        private void ThrowGenericMethod2<TKey1, TValue, TKey2, TKey3>(TKey1 key1, TValue value1, TValue value2, TKey2 key2, TKey3 key3, List<TValue> listOfTValue)
        {
            var generator = new GenericClassForValueTypeTest<int, bool>();
            generator.ThrowOneGenericFromType(true);
        }
    }

    internal class GenericClassForValueTypeTest<TKey, TValue>
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void ThrowOneGenericFromType(TValue value)
        {
            ThrowOneGenericFromMethod(value);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void ThrowOneGenericFromMethod<T>(T value)
        {
            var generator = new GenericClass<string, T>();
            generator.ThrowOneGeneric(value);
        }
    }

    internal class GenericClass<TKey, TValue>
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void ThrowOneGeneric(TValue value)
        {
            ThrowGenericFromGeneric("this is the end...");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowGenericFromGeneric<T>(T element)
        {
            ThrowFromGeneric(element, default(TKey), default(TValue), default(TKey));
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ThrowFromGeneric<T>(T element, TKey key1, TValue value, TKey key2)
        {
            try
            {
                Thread.Sleep(200);
                throw new InvalidOperationException("IOE - " + value.ToString());
            }
            catch (Exception)
            {
            }
        }
    }

    internal class MyClass
    {
        public string Member;
    }

    internal struct MyStruct
    {
        public int Member;
    }
}
#pragma warning restore SA1201 // Elements should appear in the correct order
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1402 // File may only contain a single type
