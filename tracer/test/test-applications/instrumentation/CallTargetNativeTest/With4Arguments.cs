using System.Threading.Tasks;

namespace CallTargetNativeTest
{
    // *** With4Arguments
    class With4Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    class With4ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    class With4ArgumentsInherits : With4Arguments { }
    class With4ArgumentsInheritsGeneric : With4ArgumentsGeneric<int> { }
    struct With4ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    static class With4ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    class With4ArgumentsThrowOnBegin : With4Arguments { }
    class With4ArgumentsThrowOnEnd : With4Arguments { }
}
