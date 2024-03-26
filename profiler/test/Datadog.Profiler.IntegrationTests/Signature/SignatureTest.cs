// <copyright file="SignatureTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Signature
{
    public class SignatureTest
    {
        private readonly ITestOutputHelper _output;

        public SignatureTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void ValidateSignatures(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 20");
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.TimestampsAsLabelEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();
            CheckExceptionsInProfiles(framework, exceptionSamples);
        }

        private static void CheckExceptionsInProfiles(string framework, (string Type, string Message, long Count, StackTrace Stacktrace)[] exceptionSamples)
        {
            var os = Environment.OSVersion.Platform;
            StackTrace stack;
            if (os == PlatformID.Unix && framework != "net8.0")
            {
                // BUG on Linux: invalid TKey instead of TVal
                stack = new StackTrace(
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TKey> |fn:ThrowFromGeneric |fg:<T0> |sg:(T0 element, TKey key1, TKey value, TKey key2)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TKey> |fn:ThrowGenericFromGeneric |fg:<T0> |sg:(T0 element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TKey> |fn:ThrowOneGeneric |fg: |sg:(TKey value)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClassForValueTypeTest |cg:<System.Int32, System.Boolean> |fn:ThrowOneGenericFromMethod |fg:<System.Boolean> |sg:(System.Boolean value)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClassForValueTypeTest |cg:<System.Int32, System.Boolean> |fn:ThrowOneGenericFromType |fg: |sg:(TKey value)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod2 |fg:<T0, System.Int32, T2, T3> |sg:(T0 key1, System.Int32 value1, System.Int32 value2, T2 key2, T3 key3, System.Collections.Generic.List<System.Int32> listOfTValue)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<T0> |sg:(T0 element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<Samples.Computer01.MyStruct> |sg:(Samples.Computer01.MyStruct element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<T0> |sg:(T0 element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<System.Boolean> |sg:(System.Boolean element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowWithRefs |fg: |sg:(Samples.Computer01.MyClass& mc, Samples.Computer01.MyStruct& ms)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowClass |fg: |sg:(Samples.Computer01.MyClass mc)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowStruct |fg: |sg:(Samples.Computer01.MyStruct ms)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowArrays |fg: |sg:(string[] a1, int[,,] matrix2, byte[][] jaggedArray)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowNative |fg: |sg:(nint ptr, nuint uptr)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowStringAndChar |fg: |sg:(string v, char c)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowNumbers |fg: |sg:(byte b, sbyte sb, short i16, ushort ui16, int i32, uint ui32, long i64, ulong ui64, float s, double d)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowBool |fg: |sg:(bool bValue)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowObject |fg: |sg:(object val)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowVoid |fg: |sg:()"));
            }
            else
            {
                stack = new StackTrace(
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TVal> |fn:ThrowFromGeneric |fg:<T0> |sg:(T0 element, TKey key1, TVal value, TKey key2)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TVal> |fn:ThrowGenericFromGeneric |fg:<T0> |sg:(T0 element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TVal> |fn:ThrowOneGeneric |fg: |sg:(TVal value)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClassForValueTypeTest |cg:<System.Int32, System.Boolean> |fn:ThrowOneGenericFromMethod |fg:<System.Boolean> |sg:(System.Boolean value)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClassForValueTypeTest |cg:<System.Int32, System.Boolean> |fn:ThrowOneGenericFromType |fg: |sg:(TVal value)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod2 |fg:<T0, System.Int32, T2, T3> |sg:(T0 key1, System.Int32 value1, System.Int32 value2, T2 key2, T3 key3, System.Collections.Generic.List<System.Int32> listOfTValue)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<T0> |sg:(T0 element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<Samples.Computer01.MyStruct> |sg:(Samples.Computer01.MyStruct element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<T0> |sg:(T0 element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowGenericMethod1 |fg:<System.Boolean> |sg:(System.Boolean element)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowWithRefs |fg: |sg:(Samples.Computer01.MyClass& mc, Samples.Computer01.MyStruct& ms)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowClass |fg: |sg:(Samples.Computer01.MyClass mc)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowStruct |fg: |sg:(Samples.Computer01.MyStruct ms)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowArrays |fg: |sg:(string[] a1, int[,,] matrix2, byte[][] jaggedArray)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowNative |fg: |sg:(nint ptr, nuint uptr)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowStringAndChar |fg: |sg:(string v, char c)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowNumbers |fg: |sg:(byte b, sbyte sb, short i16, ushort ui16, int i32, uint ui32, long i64, ulong ui64, float s, double d)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowBool |fg: |sg:(bool bValue)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowObject |fg: |sg:(object val)"),
                    new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |cg: |fn:ThrowVoid |fg: |sg:()"));
            }

            foreach (var sample in exceptionSamples)
            {
                sample.Message.Should().Be("IOE - False");
                sample.Type.Should().Be("System.InvalidOperationException");
                Assert.True(sample.Stacktrace.EndWith(stack));
            }
        }
    }
}
