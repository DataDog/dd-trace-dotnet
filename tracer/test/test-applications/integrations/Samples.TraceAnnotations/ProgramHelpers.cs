using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Samples.TraceAnnotations
{
    public static class ProgramHelpers
    {
        public static async Task RunTestsAsync()
        {
            var testType = new TestType();
            var testTypeName = testType.Name;

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
            testType.Finalize(0);

            // Release the reference to testType
            testType = null;

            // Force a garbage collection, try to invoke finalizer on previous testType object
            GC.Collect();

            // Delay
            await Task.Delay(500);

            var testTypeGenericString = new TestTypeGeneric<string>();
            var testTypeGenericStringName = testTypeGenericString.Name;

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
            testTypeGenericString.Finalize(0);

            // Release the reference to testTypeGenericString
            testTypeGenericString = null;

            // Force a garbage collection, try to invoke finalizer on previous testTypeGenericString object
            GC.Collect();

            // Delay
            await Task.Delay(500);

            var testTypeStruct = new TestTypeStruct();
            var testTypeStructName = testTypeStruct.Name;

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

            // Do not try to invoke finalizer for struct as it does not have a finalizer
            await Task.Delay(500);

            var testTypeStaticName = TestTypeStatic.Name;
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

            await Task.Delay(500);
            
            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
        }
    }
}
