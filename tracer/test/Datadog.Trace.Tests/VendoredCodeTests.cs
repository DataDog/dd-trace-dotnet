// <copyright file="VendoredCodeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.Tests
{
    public class VendoredCodeTests
    {
        private const BindingFlags _flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public |
                                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly ITestOutputHelper _testOutputHelper;

        public VendoredCodeTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [SkippableFact]
        public void UnsafeTypeVerifyIlTest()
        {
#if DEBUG
            throw new Xunit.SkipException("This test requires RELEASE mode and will fail in DEBUG mode on some target frameworks");
#else
            var originalType = typeof(System.Runtime.CompilerServices.Unsafe);
            var vendoredType = typeof(VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe);

            var framework = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkDisplayName;
            var frameworks = new[] { ".NET Framework 4.6.2", ".NET 5.0", ".NET 6.0", ".NET Core 3.1" };
            // TTo BitCast TFrom, TTo (TFrom source) is new in .NET 8
            _testOutputHelper.WriteLine(framework);
            if (frameworks.All(f => f != framework))
            {
                // ref assemblies
                return;
            }

            var original = originalType.GetMethods(_flags).
                                        ToDictionary(KeySelector(), ValueSelector()).
                                        OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();

            var vendored = vendoredType.GetMethods(_flags).
                                        ToDictionary(KeySelector(), ValueSelector()).
                                        OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();

            _testOutputHelper.WriteLine(string.Empty);
            _testOutputHelper.WriteLine($"Number of original methods: {original.Length}");
            _testOutputHelper.WriteLine($"Number of vendored methods: {vendored.Length}");

            _testOutputHelper.WriteLine(string.Empty);
            _testOutputHelper.WriteLine("---");
            _testOutputHelper.WriteLine("Original");
            _testOutputHelper.WriteLine("---");
            foreach (var keyValuePair in original)
            {
                _testOutputHelper.WriteLine($"{keyValuePair.Key} {keyValuePair.Value}");
            }

            _testOutputHelper.WriteLine(string.Empty);
            _testOutputHelper.WriteLine("---");
            _testOutputHelper.WriteLine("Vendored");
            _testOutputHelper.WriteLine("---");
            foreach (var keyValuePair in vendored)
            {
                _testOutputHelper.WriteLine($"{keyValuePair.Key} {keyValuePair.Value}");
            }

            int vendoredIndex = 0;

            foreach (var orderedOriginalPair in original)
            {
                for (var j = vendoredIndex; j < vendored.Length; j++)
                {
                    var orderedVendoredPair = vendored[j];
                    if (!orderedVendoredPair.Key.Equals(orderedOriginalPair.Key))
                    {
                        _testOutputHelper.WriteLine($"Skip {orderedVendoredPair.Key} method because it does not exist in the original class");
                        continue;
                    }

                    vendoredIndex = j + 1;

                    try
                    {
                        Assert.Equal(orderedOriginalPair.Value, orderedVendoredPair.Value);
                        break;
                    }
                    catch (EqualException)
                    {
                        _testOutputHelper.WriteLine(string.Empty);
                        _testOutputHelper.WriteLine("First not equal");
                        _testOutputHelper.WriteLine($"Original: {orderedOriginalPair.Key}: {orderedOriginalPair.Value}");
                        _testOutputHelper.WriteLine($"Vendored: {orderedVendoredPair.Key}: {orderedVendoredPair.Value}");
                        throw;
                    }
                }
            }

            Func<MethodInfo, string> KeySelector()
            {
                return m => $"{m.ReturnType.Name} {m.Name} {string.Join(", ", m.GetGenericArguments()?.Select(g => g.Name).ToArray())} ({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToArray())})";
            }

            Func<MethodInfo, string> ValueSelector()
            {
                return m => GetILMethodTokenAgnostic(m, out var ilBody);
            }
#endif
        }

        [Fact]
        public void UnsafeTypePrepareMethodTest()
        {
            var vendoredType = typeof(VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe);
            var allMethods = vendoredType.GetMethods(_flags).Cast<MethodBase>().Union(vendoredType.GetConstructors(_flags)).ToList();

            foreach (var method in allMethods)
            {
                List<Type> genericMethodArguments = null;
                var copyMethod = method.IsGenericMethodDefinition ? MakeGenericMethod(method, out genericMethodArguments) : method;

                if (genericMethodArguments == null || genericMethodArguments.Count == 0)
                {
                    RuntimeHelpers.PrepareMethod(copyMethod.MethodHandle);
                }
                else
                {
                    RuntimeHelpers.PrepareMethod(copyMethod.MethodHandle, genericMethodArguments.Select(inst => inst.TypeHandle).ToArray());
                }
            }
        }

        [Fact]
        public void UnsafeTypeReflectionInvokeTest()
        {
            var vendoredType = typeof(VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe);
            var allMethods = vendoredType.GetMethods(_flags).Cast<MethodBase>().Union(vendoredType.GetConstructors(_flags)).ToList();
            foreach (var method in allMethods)
            {
                List<Type> genericMethodArguments = null;
                var copyMethod = method.IsGenericMethodDefinition ? MakeGenericMethod(method, out genericMethodArguments) : method;
                GCHandle handle = default;
                try
                {
                    copyMethod.Invoke(
                        null,
                        copyMethod.GetParameters()
                                  .Select(
                                       p =>
                                       {
                                           object o = null;
                                           var elementType = p.ParameterType.GetElementType();
                                           // instead of handle constructor parameters
                                           if (elementType != null)
                                           {
#if NET5_0_OR_GREATER
                                               o = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(elementType == typeof(void) ? typeof(IntPtr) : elementType);
#else
                                               o = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(elementType);
#endif
                                           }
                                           else
                                           {
                                               // in this test only parameter-less constructors reaches this line
                                               if (copyMethod.Name == "Unbox")
                                               {
                                                   // ref T Unbox<T>(object box) where T : struct
                                                   o = Activator.CreateInstance(typeof(int));
                                               }
                                               else
                                               {
                                                   o = Activator.CreateInstance(p.ParameterType);
                                               }
                                           }

                                           if (p.ParameterType.IsPointer)
                                           {
                                               handle = GCHandle.Alloc(o, GCHandleType.Pinned);
                                               o = handle.AddrOfPinnedObject();
                                           }

                                           return o;
                                       })
                                  .ToArray());
                }
                catch (System.NotSupportedException e) when (e.Message == "ByRef return value not supported in reflection invocation.")
                {
                    // skip
                    // for the test we don't care about return value.
                }
                catch (NullReferenceException e) when (e.Message == "The target method returned a null reference." && copyMethod.Name == "NullRef")
                {
                    // skip
                }
                catch (Exception e) when (e.InnerException is NullReferenceException && e.InnerException.Message == "The target method returned a null reference." && copyMethod.Name == "NullRef")
                {
                    // skip
                }
                finally
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
        }

        private static MethodBase MakeGenericMethod(MethodBase foundedMb, out List<Type> genericMethodArguments)
        {
            genericMethodArguments = new List<Type>();
            foreach (var argument in foundedMb.GetGenericArguments())
            {
                var constraints = argument.GetGenericParameterConstraints().ToList();
                if (constraints.Any())
                {
                    var constraintType = constraints[0];
                    genericMethodArguments.Add(constraintType.IsValueType || constraintType == typeof(ValueType) ? typeof(int) : constraintType);
                }
                else
                {
                    genericMethodArguments.Add(typeof(object));
                }
            }

            return ((MethodInfo)foundedMb).MakeGenericMethod(genericMethodArguments.ToArray());
        }

        private string GetILMethodTokenAgnostic(MethodBase method, out byte[] ilBody)
        {
            if (GetIlBody(method, out ilBody) == false)
            {
                return null;
            }

            var reader = new TokenIgnoringILReader(method, ilBody);
            var bytesForHash = new List<byte>();
            foreach (var bytes in reader)
            {
                bytesForHash.AddRange(bytes);
            }

            return Convert.ToBase64String(bytesForHash.ToArray());
        }

        private bool GetIlBody(MethodBase method, out byte[] ilBody)
        {
            try
            {
                ilBody = method.GetMethodBody()?.GetILAsByteArray();
                if (ilBody == null)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                ilBody = null;
                return false;
            }

            return true;
        }

        private sealed class TokenIgnoringILReader : IEnumerable<List<byte>>
        {
            private static readonly Type RuntimeMethodInfoType = Type.GetType("System.Reflection.RuntimeMethodInfo");
            private static readonly Type RuntimeConstructorInfoType = Type.GetType("System.Reflection.RuntimeConstructorInfo");

            private static readonly OpCode[] OneByteOpCodes;
            private static readonly OpCode[] TwoByteOpCodes;
            private readonly byte[] _byteArray;
            private int _position;

            static TokenIgnoringILReader()
            {
                OneByteOpCodes = new OpCode[0x100];
                TwoByteOpCodes = new OpCode[0x100];

                foreach (FieldInfo fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    OpCode opCode = (OpCode)fi.GetValue(null);
                    ushort value = (ushort)opCode.Value;
                    if (value < 0x100)
                    {
                        OneByteOpCodes[value] = opCode;
                    }
                    else if ((value & 0xff00) == 0xfe00)
                    {
                        TwoByteOpCodes[value & 0xff] = opCode;
                    }
                }
            }

            public TokenIgnoringILReader(MethodBase method, byte[] code)
            {
                if (method == null)
                {
                    throw new ArgumentNullException(nameof(method));
                }

                Type rtType = method.GetType();
                if (rtType != RuntimeMethodInfoType && rtType != RuntimeConstructorInfoType)
                {
                    throw new ArgumentException("method must be RuntimeMethodInfo or RuntimeConstructorInfo.");
                }

                _byteArray = code;
                _position = 0;
            }

            public IEnumerator<List<byte>> GetEnumerator()
            {
                while (_position < _byteArray.Length)
                {
                    yield return Next();
                }

                _position = 0;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private List<byte> Next()
            {
                OpCode opCode;

                // read first 1 or 2 bytes as opCode
                byte code = ReadByte();
                if (code != 0xFE)
                {
                    opCode = OneByteOpCodes[code];
                }
                else
                {
                    code = ReadByte();
                    opCode = TwoByteOpCodes[code];
                }

                List<byte> bytes = new List<byte> { code };
                switch (opCode.OperandType)
                {
                    // The operand is an 8-bit integer branch target.
                    case OperandType.ShortInlineBrTarget:
                        sbyte shortDelta = ReadSByte();
                        bytes.Add((byte)shortDelta);
                        return bytes;

                    // The operand is a 32-bit integer branch target.
                    case OperandType.InlineBrTarget:
                        int delta = ReadInt32();
                        bytes.AddRange(BitConverter.GetBytes(delta));
                        return bytes;

                    // The operand is an 8-bit integer: 001F  ldc.i4.s, FE12  unaligned.
                    case OperandType.ShortInlineI:
                        byte int8 = ReadByte();
                        bytes.Add(int8);
                        return bytes;

                    // The operand is a 32-bit integer.
                    case OperandType.InlineI:
                        int int32 = ReadInt32();
                        bytes.AddRange(BitConverter.GetBytes(int32));
                        return bytes;

                    // The operand is a 64-bit integer.
                    case OperandType.InlineI8:
                        long int64 = ReadInt64();
                        bytes.AddRange(BitConverter.GetBytes(int64));
                        return bytes;

                    // The operand is a 32-bit IEEE floating point number.
                    case OperandType.ShortInlineR:
                        float float32 = ReadSingle();
                        bytes.AddRange(BitConverter.GetBytes(float32));
                        return bytes;

                    // The operand is a 64-bit IEEE floating point number.
                    case OperandType.InlineR:
                        double float64 = ReadDouble();
                        bytes.AddRange(BitConverter.GetBytes(float64));
                        return bytes;

                    // The operand is an 8-bit integer containing the ordinal of a local variable or an argument
                    case OperandType.ShortInlineVar:
                        byte index8 = ReadByte();
                        bytes.Add(index8);
                        return bytes;

                    // The operand is 16-bit integer containing the ordinal of a local variable or an argument.
                    case OperandType.InlineVar:
                        ushort index16 = ReadUInt16();
                        bytes.AddRange(BitConverter.GetBytes(index16));
                        return bytes;

                    // The operand is a 32-bit metadata string token.
                    case OperandType.InlineString:
                        ReadInt32();
                        bytes.Add(0);
                        return bytes;

                    // The operand is a 32-bit metadata signature token.
                    case OperandType.InlineSig:
                        ReadInt32();
                        bytes.Add(0);
                        return bytes;

                    // The operand is a 32-bit metadata token.
                    case OperandType.InlineMethod:
                        ReadInt32();
                        bytes.Add(0);
                        return bytes;

                    // The operand is a 32-bit metadata token.
                    case OperandType.InlineField:
                        ReadInt32();
                        bytes.Add(0);
                        return bytes;

                    // The operand is a 32-bit metadata token.
                    case OperandType.InlineType:
                        ReadInt32();
                        bytes.Add(0);
                        return bytes;

                    // The operand is a FieldRef, MethodRef, or TypeRef token.
                    case OperandType.InlineTok:
                        ReadInt32();
                        bytes.Add(0);
                        return bytes;

                    // The operand is the 32-bit integer argument to a switch instruction.
                    case OperandType.InlineSwitch:
                        int cases = ReadInt32();
                        bytes.AddRange(BitConverter.GetBytes(cases));

                        int[] deltas = new int[cases];
                        for (int i = 0; i < cases; i++)
                        {
                            deltas[i] = ReadInt32();
                            bytes.AddRange(BitConverter.GetBytes(deltas[i]));
                        }

                        return bytes;

                    case OperandType.InlineNone:
                        bytes.Add(0);
                        return bytes;
                    default:
                        throw new BadImageFormatException("unexpected OperandType " + opCode.OperandType);
                }
            }

            private byte ReadByte()
            {
                return (byte)_byteArray[_position++];
            }

            private sbyte ReadSByte()
            {
                return (sbyte)ReadByte();
            }

            private ushort ReadUInt16()
            {
                int pos = _position;
                _position += 2;
                return BitConverter.ToUInt16(_byteArray, pos);
            }

            private uint ReadUInt32()
            {
                int pos = _position;
                _position += 4;
                return BitConverter.ToUInt32(_byteArray, pos);
            }

            private ulong ReadUInt64()
            {
                int pos = _position;
                _position += 8;
                return BitConverter.ToUInt64(_byteArray, pos);
            }

            private int ReadInt32()
            {
                int pos = _position;
                _position += 4;
                return BitConverter.ToInt32(_byteArray, pos);
            }

            private long ReadInt64()
            {
                int pos = _position;
                _position += 8;
                return BitConverter.ToInt64(_byteArray, pos);
            }

            private float ReadSingle()
            {
                int pos = _position;
                _position += 4;
                return BitConverter.ToSingle(_byteArray, pos);
            }

            private double ReadDouble()
            {
                int pos = _position;
                _position += 8;
                return BitConverter.ToDouble(_byteArray, pos);
            }
        }
    }
}
