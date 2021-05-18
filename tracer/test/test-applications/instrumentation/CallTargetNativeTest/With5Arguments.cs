using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest
{
    // *** With5Arguments
    class With5Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    class With5ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    class With5ArgumentsInherits : With5Arguments { }
    class With5ArgumentsInheritsGeneric : With5ArgumentsGeneric<int> { }
    struct With5ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    static class With5ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    class With5ArgumentsThrowOnBegin : With5Arguments { }
    class With5ArgumentsThrowOnEnd : With5Arguments { }
}
