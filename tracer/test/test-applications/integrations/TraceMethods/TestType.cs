using System.Threading.Tasks;

namespace TraceMethods
{
    internal class TestType
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.CompletedTask;
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
    }
    class TestTypeGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.CompletedTask;
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
    }
    struct TestTypeStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.CompletedTask;
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
    }
    static class TestTypeStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public static Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.CompletedTask;
        public static Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
    }
}
