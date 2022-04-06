using System.Threading.Tasks;

namespace Samples.TraceAnnotations
{
    internal class TestType
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
    class TestTypeGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
    struct TestTypeStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
    static class TestTypeStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public static Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public static Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public static ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public static ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
}
