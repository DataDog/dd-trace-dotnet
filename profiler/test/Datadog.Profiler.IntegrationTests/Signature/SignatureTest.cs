using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            // disable default profilers
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.TimestampsAsLabelEnabled, "0");

            runner.Environment.SetVariable(EnvironmentVariables.ExceptionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);
            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var exceptionSamples = SamplesHelper.ExtractExceptionSamples(runner.Environment.PprofDir).ToArray();
            CheckExceptionsInProfiles(exceptionSamples);
        }

        private void CheckExceptionsInProfiles((string Type, string Message, long Count, StackTrace Stacktrace)[] exceptionSamples)
        {
            var stack = new StackTrace(
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass{|ns: |ct:TKey, |ns: |ct:TVal} |fn:ThrowFromGeneric{|ns: |ct:T0} |sg:(T0 element, TKey key1, TVal value, TKey key2)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass{|ns: |ct:TKey, |ns: |ct:TVal} |fn:ThrowGenericFromGeneric{|ns: |ct:T0} |sg:(T0 element)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass{|ns: |ct:TKey, |ns: |ct:TVal} |fn:ThrowOneGeneric |sg:(TVal value)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClassForValueTypeTest{|ns:System |ct:Int32, |ns:System |ct:Boolean} |fn:ThrowOneGenericFromMethod{|ns:System |ct:Boolean} |sg:(System.Boolean value)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClassForValueTypeTest{|ns:System |ct:Int32, |ns:System |ct:Boolean} |fn:ThrowOneGenericFromType |sg:(TVal value)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowGenericMethod2{|ns: |ct:T0, |ns:System |ct:Int32, |ns: |ct:T2, |ns: |ct:T3} |sg:(T0 key1, System.Int32 value1, System.Int32 value2, T2 key2, T3 key3, System.Collections.Generic.List<System.Int32> listOfTValue)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowGenericMethod1{|ns: |ct:T0} |sg:(T0 element)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowGenericMethod1{|ns:Samples.Computer01 |ct:MyStruct} |sg:(Samples.Computer01.MyStruct element)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowGenericMethod1{|ns: |ct:T0} |sg:(T0 element)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowGenericMethod1{|ns:System |ct:Boolean} |sg:(System.Boolean element)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowWithRefs |sg:(Samples.Computer01.MyClass& mc, Samples.Computer01.MyStruct& ms)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowClass |sg:(Samples.Computer01.MyClass mc)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowStruct |sg:(Samples.Computer01.MyStruct ms)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowArrays |sg:(string[] a1, int[,,] matrix2, byte[][] jaggedArray)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowNative |sg:(nint ptr, nuint uptr)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowStringAndChar |sg:(string v, char c)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowNumbers |sg:(byte b, sbyte sb, short i16, ushort ui16, int i32, uint ui32, long i64, ulong ui64, float s, double d)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowBool |sg:(bool bValue)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowObject |sg:(object val)"),
                new StackFrame("|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:MethodsSignature |fn:ThrowVoid |sg:()"));

            foreach (var sample in exceptionSamples)
            {
                sample.Message.Should().Be("IOE - False");
                sample.Type.Should().Be("System.InvalidOperationException");
                Assert.True(sample.Stacktrace.EndWith(stack));
            }
        }
    }
}
