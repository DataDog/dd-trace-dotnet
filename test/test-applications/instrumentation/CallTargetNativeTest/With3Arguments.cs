namespace CallTargetNativeTest
{
    // *** With3Arguments
    class With3Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    class With3ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    class With3ArgumentsInherits : With3Arguments { }
    class With3ArgumentsInheritsGeneric : With3ArgumentsGeneric<int> { }
    struct With3ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    static class With3ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
}
