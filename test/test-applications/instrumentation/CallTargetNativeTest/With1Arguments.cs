namespace CallTargetNativeTest
{
    // *** With1Arguments
    class With1Arguments
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    class With1ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
    }
    class With1ArgumentsInherits : With1Arguments { }
    class With1ArgumentsInheritsGeneric : With1ArgumentsGeneric<int> { }
    struct With1ArgumentsStruct
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    static class With1ArgumentsStatic
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
}
