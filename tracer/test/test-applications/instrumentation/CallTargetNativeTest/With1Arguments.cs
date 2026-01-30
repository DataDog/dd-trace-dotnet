using System;
using CallTargetNativeTest.NoOp;

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
        Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => w1.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
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
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => w1g1.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
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
        Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => w1g2.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<string>).FullName}.VoidMethod");
        RunMethod(() => With1ArgumentsGenericStatic<string>.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<string>).FullName}.ReturnValueMethod");
        RunMethod(() => With1ArgumentsGenericStatic<string>.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => With1ArgumentsGenericStatic<string>.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => With1ArgumentsGenericStatic<string>.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<int>).FullName}.VoidMethod");
        RunMethod(() => With1ArgumentsGenericStatic<int>.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<int>).FullName}.ReturnValueMethod");
        RunMethod(() => With1ArgumentsGenericStatic<int>.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => With1ArgumentsGenericStatic<int>.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(With1ArgumentsGenericStatic<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => With1ArgumentsGenericStatic<int>.ReturnGenericMethod<int>(42));
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
        Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => w1in.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
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
        Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => w1inGen.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
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
        Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => w1Struct.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
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
        Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnValueMethod (SKIPPING METHOD BODY)");
        RunMethod(() => With1ArgumentsStatic.ReturnValueMethod(Noop1ArgumentsIntegration.SKIPMETHODBODY));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With1ArgumentsStaticStruct).FullName}.VoidMethod");
        RunMethod(() => With1ArgumentsStaticStruct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStaticStruct).FullName}.ReturnValueMethod");
        RunMethod(() => With1ArgumentsStaticStruct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStaticStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => With1ArgumentsStaticStruct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(With1ArgumentsStaticStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With1ArgumentsStaticStruct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(With1ArgumentsStaticStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With1ArgumentsStaticStruct.ReturnGenericMethod<int, string>("Hello World"));
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
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<string>).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<string>.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<string>).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<string>.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<string>.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<string>.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<int>).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<int>.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<int>).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<int>.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<int>.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsGenericStatic<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsGenericStatic<int>.ReturnGenericMethod<int>(42));
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
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStaticStruct).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStaticStruct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStaticStruct).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStaticStruct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStaticStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStaticStruct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStaticStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStaticStruct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With1ArgumentsStaticStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With1ArgumentsStaticStruct.ReturnGenericMethod<int, string>("Hello World"));
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
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<string>).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<string>.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<string>).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<string>.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<string>.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<string>.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<int>).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<int>.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<int>).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<int>.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<int>.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsGenericStatic<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsGenericStatic<int>.ReturnGenericMethod<int>(42));
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
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStaticStruct).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStaticStruct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStaticStruct).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStaticStruct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStaticStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStaticStruct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStaticStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStaticStruct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With1ArgumentsStaticStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With1ArgumentsStaticStruct.ReturnGenericMethod<int, string>("Hello World"));
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

    private static void GenericParentArgument1()
    {
        var w1 = new ArgumentsGenericParentType<object>.With1Arguments();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1Arguments).FullName}.VoidMethod");
        RunMethod(() => w1.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w1.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1g1 = new ArgumentsGenericParentType<object>.With1ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w1g1.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g1.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g1.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g1.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        var w1g2 = new ArgumentsGenericParentType<object>.With1ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w1g2.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w1g2.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1g2.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w1g2.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>).FullName}.VoidMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<string>.ReturnGenericMethod<string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>).FullName}.VoidMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsGenericStatic<int>.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1in = new ArgumentsGenericParentType<object>.With1ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w1in.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w1in.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1in.ReturnReferenceMethod("Hello Wolrd"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1in.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1in.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1inGen = new ArgumentsGenericParentType<object>.With1ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w1inGen.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w1inGen.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1inGen.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w1inGen.ReturnGenericMethod<int>(42));
        Console.WriteLine();
        //
        var w1Struct = new ArgumentsGenericParentType<object>.With1ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w1Struct.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w1Struct.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1Struct.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1Struct.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1Struct.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsStatic.VoidMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsStatic.ReturnValueMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsStatic.ReturnReferenceMethod("Hello World"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsStatic.ReturnGenericMethod<string, int>(42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsGenericParentType<object>.With1ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World"));
        Console.WriteLine();
        //
        var w1TBegin = new ArgumentsGenericParentType<object>.With1ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w1TBegin.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w1TBegin.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TBegin.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w1TBegin.ReturnGenericMethod<int, int>(42));
        Console.WriteLine();
        //
        var w1TEnd = new ArgumentsGenericParentType<object>.With1ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w1TEnd.VoidMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w1TEnd.ReturnValueMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w1TEnd.ReturnReferenceMethod("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w1TEnd.ReturnGenericMethod<string, string>("Hello world"));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
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
static class With1ArgumentsGenericStatic<T>
{
    public static void VoidMethod(string arg1) { }
    public static int ReturnValueMethod(string arg1) => 42;
    public static string ReturnReferenceMethod(string arg1) => "Hello World";
    public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
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
struct With1ArgumentsStaticStruct
{
    public static void VoidMethod(string arg1) { }
    public static int ReturnValueMethod(string arg1) => 42;
    public static string ReturnReferenceMethod(string arg1) => "Hello World";
    public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
}
struct With1ArgumentsGenericStaticStruct<T> // Note: This type cannot be instrumented because it is a generic struct.
{
    public static void VoidMethod(string arg1) { }
    public static int ReturnValueMethod(string arg1) => 42;
    public static string ReturnReferenceMethod(string arg1) => "Hello World";
    public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
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
    public static class With1ArgumentsGenericStatic<T>
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
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
    public struct With1ArgumentsStaticStruct
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public struct With1ArgumentsGenericStaticStruct<T> // Note: This type cannot be instrumented because it is a generic struct.
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
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
    public static class With1ArgumentsGenericStatic<T>
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
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
    public struct With1ArgumentsStaticStruct
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public struct With1ArgumentsGenericStaticStruct<T> // Note: This type cannot be instrumented because it is a generic struct.
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsThrowOnBegin : With1Arguments { }
    public class With1ArgumentsThrowOnEnd : With1Arguments { }
}

partial class ArgumentsGenericParentType<PType>
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
    public static class With1ArgumentsGenericStatic<T>
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
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
    public struct With1ArgumentsStaticStruct // Note: This type cannot be instrumented because it is a struct and we are unable to get the type when the parent type is generic.
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    public struct With1ArgumentsGenericStaticStruct<T> // Note: This type cannot be instrumented because it is a generic struct.
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
    }
    public class With1ArgumentsThrowOnBegin : With1Arguments { }
    public class With1ArgumentsThrowOnEnd : With1Arguments { }
}
