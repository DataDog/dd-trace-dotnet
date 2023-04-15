using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Annotations;

namespace Samples.TraceAnnotations
{
    public static class ProgramHelpers
    {
        [Trace]
        public static async Task RunTestsAsync()
        {
            Console.WriteLine("start");
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

            Console.WriteLine("end");

        }

        [Trace(OperationName = "overridden.attribute", ResourceName = "Program_WaitUsingOfficialAttribute")]
        private static Task WaitUsingOfficialAttribute() => Task.Delay(500);
    }
}
