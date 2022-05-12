extern alias OfficialDatadogAlias;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Api.Agent;
using CustomTraceAttribute = Datadog.Trace.Annotations.TraceAttribute;
using NewRelicTraceAttribute = NewRelic.Api.Agent.TraceAttribute;
using OfficialTraceAttribute = OfficialDatadogAlias::Datadog.Trace.Annotations.TraceAttribute;

namespace Samples.TraceAnnotations
{
    public static class ProgramHelpers
    {
        [CustomTrace]
        public static async Task RunTestsAsync()
        {
            // Invoke instrumented methods for reference type
            var testType = new TestType();
            var testTypeName = testType.Name;
            testType.Name = null;

            testType.VoidMethod("Hello world", 42, Tuple.Create(1, 2));
            testType.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2));
            testType.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2));
            testType.ReturnNullMethod("Hello world", 42, Tuple.Create(1, 2));
            testType.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2));
            testType.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2));
            await testType.ReturnTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await testType.ReturnTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            await testType.ReturnValueTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await testType.ReturnValueTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            testType.ReturnGenericMethodAttribute<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2));
            testType.ExtensionMethodForTestType();
            testType.Finalize(0);

            // Release the reference to testType
            // Force a garbage collection, try to invoke finalizer on previous testType object
            testType = null;
            GC.Collect();

            // Delay
            await Task.Delay(500);

            // Invoke instrumented methods for generic reference type
            var testTypeGenericString = new TestTypeGeneric<string>();
            var testTypeGenericStringName = testTypeGenericString.Name;
            testTypeGenericString.Name = null;

            testTypeGenericString.VoidMethod("Hello World", 42, Tuple.Create(1, 2));
            testTypeGenericString.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2));
            testTypeGenericString.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2));
            testTypeGenericString.ReturnNullMethod("Hello world", 42, Tuple.Create(1, 2));
            testTypeGenericString.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2));
            //
            await testTypeGenericString.ReturnTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await testTypeGenericString.ReturnTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            await testTypeGenericString.ReturnValueTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await testTypeGenericString.ReturnValueTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            testTypeGenericString.ReturnGenericMethodAttribute<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2));
            testTypeGenericString.ExtensionMethodForTestTypeGeneric();
            testTypeGenericString.Finalize(0);

            // Release the reference to testTypeGenericString
            // Force a garbage collection, try to invoke finalizer on previous testTypeGenericString object
            testTypeGenericString = null;
            GC.Collect();

            // Delay
            await Task.Delay(500);

            // Invoke instrumented methods for value type
            var testTypeStruct = new TestTypeStruct();
            var testTypeStructName = testTypeStruct.Name;
            testTypeStruct.Name = null;

            testTypeStruct.VoidMethod("Hello World", 42, Tuple.Create(1, 2));
            testTypeStruct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2));
            testTypeStruct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2));
            testTypeStruct.ReturnNullMethod("Hello world", 42, Tuple.Create(1, 2));
            testTypeStruct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2));
            testTypeStruct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2));
            await testTypeStruct.ReturnTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await testTypeStruct.ReturnTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            await testTypeStruct.ReturnValueTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await testTypeStruct.ReturnValueTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            testTypeStruct.ReturnGenericMethodAttribute<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2));
            testTypeStruct.ExtensionMethodForTestTypeTypeStruct();

            // Delay
            await Task.Delay(500);

            // Invoke instrumented methods for static type
            var testTypeStaticName = TestTypeStatic.Name;
            TestTypeStatic.Name = null;
            TestTypeStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2));
            TestTypeStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2));
            TestTypeStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2));
            TestTypeStatic.ReturnNullMethod("Hello world", 42, Tuple.Create(1, 2));
            TestTypeStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2));
            TestTypeStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2));
            await TestTypeStatic.ReturnTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await TestTypeStatic.ReturnTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            await TestTypeStatic.ReturnValueTaskMethod("Hello world", 42, Tuple.Create(1, 2));
            await TestTypeStatic.ReturnValueTaskTMethod("Hello world", 42, Tuple.Create(1, 2));
            TestTypeStatic.ReturnGenericMethodAttribute<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2));

            // Delay
            await Task.Delay(500);

            // Invoke instrumented methods on framework types
            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;

            // Delay
            await Task.Delay(500);

            // Invoke instrumented methods only from attributes
            await AttributeOnlyStatic.ReturnTaskTMethod("Hello World", 42, Tuple.Create(1, 2));
            await WaitUsingOfficialAttribute();
            await NewRelicTransactionMethodAsync("Hello World");
            NewRelicTraceMethod(42);
        }

        [OfficialTrace(OperationName = "overridden.attribute", ResourceName = "Program_WaitUsingOfficialAttribute")]
        private static Task WaitUsingOfficialAttribute() => Task.Delay(500);

        [Transaction]
        private static Task NewRelicTransactionMethodAsync(string input) => Task.Delay(500);

        [NewRelicTrace]
        private static void NewRelicTraceMethod(int input) { }
    }
}
