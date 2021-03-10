namespace CallTargetNativeTest
{
    // *** With0Arguments
    class With0Arguments
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    class With0ArgumentsGeneric<T>
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod() => default;
    }
    class With0ArgumentsInherits : With0Arguments { }
    class With0ArgumentsInheritsGeneric : With0ArgumentsGeneric<int> { }
    struct With0ArgumentsStruct
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    static class With0ArgumentsStatic
    {
        public static void VoidMethod() { }
        public static int ReturnValueMethod() => 42;
        public static string ReturnReferenceMethod() => "Hello World";
        public static T ReturnGenericMethod<T>() => default;
    }

}
