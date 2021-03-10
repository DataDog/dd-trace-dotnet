namespace CallTargetNativeTest
{
    // *** With2Arguments
    class With2Arguments
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg, int arg21) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    class With2ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1, int arg2) => default;
    }
    class With2ArgumentsInherits : With2Arguments { }
    class With2ArgumentsInheritsGeneric : With2ArgumentsGeneric<int> { }
    struct With2ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    static class With2ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2) { }
        public static int ReturnValueMethod(string arg1, int arg2) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
}
