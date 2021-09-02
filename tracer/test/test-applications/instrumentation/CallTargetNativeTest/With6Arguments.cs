using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest
{
    // *** With6Arguments
    class With6Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    class With6ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    class With6ArgumentsInherits : With6Arguments { }
    class With6ArgumentsInheritsGeneric : With6ArgumentsGeneric<int> { }
    struct With6ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    static class With6ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    class With6ArgumentsThrowOnBegin : With6Arguments { }
    class With6ArgumentsThrowOnEnd : With6Arguments { }
}
