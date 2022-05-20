using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument1()
    {
        var w1 = new With1Arguments();
        Console.WriteLine($"{typeof(With1Arguments).FullName}.VoidMethod");
        RunMethod(() => w1.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w1.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1g1 = new With1ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w1g1.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g1.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g1.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g1.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        var w1g2 = new With1ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w1g2.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g2.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g2.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g2.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1in = new With1ArgumentsInherits();
        Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w1in.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w1in.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1in.ReturnReferenceMethod("Hello Wolrd"));
        Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1in.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1in.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1inGen = new With1ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w1inGen.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w1inGen.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1inGen.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w1inGen.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1Struct = new With1ArgumentsStruct();
        Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w1Struct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w1Struct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1Struct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1Struct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1Struct.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With1ArgumentsStatic.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With1ArgumentsStatic.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With1ArgumentsStatic.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With1ArgumentsStatic.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With1ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1TBegin = new With1ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w1TBegin.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w1TBegin.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TBegin.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1TEnd = new With1ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w1TEnd.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w1TEnd.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TEnd.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
    }

    private static void ParentArgument1()
    {
        var w1 = new ArgumentsParentType.With1Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1Arguments).FullName}.VoidMethod");
        RunMethod(() => w1.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w1.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1g1 = new ArgumentsParentType.With1ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w1g1.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g1.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g1.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g1.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        var w1g2 = new ArgumentsParentType.With1ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w1g2.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g2.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g2.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g2.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1in = new ArgumentsParentType.With1ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w1in.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w1in.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1in.ReturnReferenceMethod("Hello Wolrd"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1in.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1in.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1inGen = new ArgumentsParentType.With1ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w1inGen.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w1inGen.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1inGen.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w1inGen.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1Struct = new ArgumentsParentType.With1ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w1Struct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w1Struct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1Struct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1Struct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1Struct.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStatic.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStatic.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStatic.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStatic.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1TBegin = new ArgumentsParentType.With1ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w1TBegin.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w1TBegin.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TBegin.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1TEnd = new ArgumentsParentType.With1ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w1TEnd.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w1TEnd.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TEnd.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
    }

    private static void StructParentArgument1()
    {
        var w1 = new ArgumentsStructParentType.With1Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1Arguments).FullName}.VoidMethod");
        RunMethod(() => w1.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w1.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1g1 = new ArgumentsStructParentType.With1ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w1g1.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g1.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g1.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g1.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        var w1g2 = new ArgumentsStructParentType.With1ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w1g2.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g2.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g2.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g2.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1in = new ArgumentsStructParentType.With1ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w1in.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w1in.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1in.ReturnReferenceMethod("Hello Wolrd"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1in.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1in.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1inGen = new ArgumentsStructParentType.With1ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w1inGen.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w1inGen.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1inGen.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w1inGen.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1Struct = new ArgumentsStructParentType.With1ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w1Struct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w1Struct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1Struct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1Struct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1Struct.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStatic.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStatic.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStatic.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStatic.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1TBegin = new ArgumentsStructParentType.With1ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w1TBegin.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w1TBegin.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TBegin.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1TEnd = new ArgumentsStructParentType.With1ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w1TEnd.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w1TEnd.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TEnd.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
    }
}

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
class With1ArgumentsThrowOnBegin : With1Arguments { }
class With1ArgumentsThrowOnEnd : With1Arguments { }

partial class ArgumentsParentType
{
    public class With1Arguments
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsInherits : With1Arguments { }
    public class With1ArgumentsInheritsGeneric : With1ArgumentsGeneric<int> { }
    public struct With1ArgumentsStruct
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public static class With1ArgumentsStatic
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsThrowOnBegin : With1Arguments { }
    public class With1ArgumentsThrowOnEnd : With1Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With1Arguments
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsInherits : With1Arguments { }
    public class With1ArgumentsInheritsGeneric : With1ArgumentsGeneric<int> { }
    public struct With1ArgumentsStruct
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public static class With1ArgumentsStatic
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsThrowOnBegin : With1Arguments { }
    public class With1ArgumentsThrowOnEnd : With1Arguments { }
}
