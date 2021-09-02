using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest
{
    // *** With9Arguments
    class With9Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    class With9ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    class With9ArgumentsInherits : With9Arguments { }
    class With9ArgumentsInheritsGeneric : With9ArgumentsGeneric<int> { }
    struct With9ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    static class With9ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    class With9ArgumentsThrowOnBegin : With9Arguments { }
    class With9ArgumentsThrowOnEnd : With9Arguments { }
}
