using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument2()
    {
        var w2 = new With2Arguments();
        Console.WriteLine($"{typeof(With2Arguments).FullName}.VoidMethod");
        RunMethod(() => w2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2g1 = new With2ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w2g1.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g1.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g1.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g1.ReturnGenericMethod<string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2g2 = new With2ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w2g2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g2.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2in = new With2ArgumentsInherits();
        Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w2in.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w2in.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2in.ReturnReferenceMethod("Hello Wolrd", 42));
        Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2in.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2in.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2inGen = new With2ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w2inGen.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w2inGen.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2inGen.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w2inGen.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2Struct = new With2ArgumentsStruct();
        Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w2Struct.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w2Struct.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2Struct.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2Struct.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2Struct.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With2ArgumentsStatic.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With2ArgumentsStatic.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With2ArgumentsStatic.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With2ArgumentsStatic.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With2ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2TBegin = new With2ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w2TBegin.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w2TBegin.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TBegin.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2TEnd = new With2ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w2TEnd.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w2TEnd.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TEnd.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
    }

    private static void ParentArgument2()
    {
        var w2 = new ArgumentsParentType.With2Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2Arguments).FullName}.VoidMethod");
        RunMethod(() => w2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2g1 = new ArgumentsParentType.With2ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w2g1.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g1.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g1.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g1.ReturnGenericMethod<string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2g2 = new ArgumentsParentType.With2ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w2g2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g2.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2in = new ArgumentsParentType.With2ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w2in.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w2in.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2in.ReturnReferenceMethod("Hello Wolrd", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2in.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2in.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2inGen = new ArgumentsParentType.With2ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w2inGen.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w2inGen.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2inGen.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w2inGen.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2Struct = new ArgumentsParentType.With2ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w2Struct.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w2Struct.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2Struct.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2Struct.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2Struct.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With2ArgumentsStatic.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With2ArgumentsStatic.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With2ArgumentsStatic.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With2ArgumentsStatic.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With2ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2TBegin = new ArgumentsParentType.With2ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w2TBegin.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w2TBegin.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TBegin.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2TEnd = new ArgumentsParentType.With2ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w2TEnd.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w2TEnd.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TEnd.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
    }

    private static void StructParentArgument2()
    {
        var w2 = new ArgumentsStructParentType.With2Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2Arguments).FullName}.VoidMethod");
        RunMethod(() => w2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2g1 = new ArgumentsStructParentType.With2ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w2g1.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g1.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g1.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g1.ReturnGenericMethod<string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2g2 = new ArgumentsStructParentType.With2ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w2g2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g2.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2in = new ArgumentsStructParentType.With2ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w2in.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w2in.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2in.ReturnReferenceMethod("Hello Wolrd", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2in.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2in.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2inGen = new ArgumentsStructParentType.With2ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w2inGen.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w2inGen.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2inGen.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w2inGen.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2Struct = new ArgumentsStructParentType.With2ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w2Struct.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w2Struct.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2Struct.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2Struct.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2Struct.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With2ArgumentsStatic.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With2ArgumentsStatic.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With2ArgumentsStatic.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With2ArgumentsStatic.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With2ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2TBegin = new ArgumentsStructParentType.With2ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w2TBegin.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w2TBegin.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TBegin.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2TEnd = new ArgumentsStructParentType.With2ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w2TEnd.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w2TEnd.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TEnd.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
    }

    private static void GenericParentArgument2()
    {
        var w2 = new ArgumentsGenericParentType<object>.With2Arguments();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2Arguments).FullName}.VoidMethod");
        RunMethod(() => w2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2g1 = new ArgumentsGenericParentType<object>.With2ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w2g1.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g1.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g1.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g1.ReturnGenericMethod<string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2g2 = new ArgumentsGenericParentType<object>.With2ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w2g2.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w2g2.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2g2.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w2g2.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2in = new ArgumentsGenericParentType<object>.With2ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w2in.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w2in.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2in.ReturnReferenceMethod("Hello Wolrd", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2in.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2in.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2inGen = new ArgumentsGenericParentType<object>.With2ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w2inGen.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w2inGen.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2inGen.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w2inGen.ReturnGenericMethod<int>(42, 99));
        Console.WriteLine();
        //
        var w2Struct = new ArgumentsGenericParentType<object>.With2ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w2Struct.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w2Struct.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2Struct.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2Struct.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2Struct.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With2ArgumentsStatic.VoidMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With2ArgumentsStatic.ReturnValueMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With2ArgumentsStatic.ReturnReferenceMethod("Hello World", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsGenericParentType<object>.With2ArgumentsStatic.ReturnGenericMethod<string, int>(42, 99));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsGenericParentType<object>.With2ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World", 42));
        Console.WriteLine();
        //
        var w2TBegin = new ArgumentsGenericParentType<object>.With2ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w2TBegin.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w2TBegin.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TBegin.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TBegin.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
        //
        var w2TEnd = new ArgumentsGenericParentType<object>.With2ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w2TEnd.VoidMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w2TEnd.ReturnValueMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w2TEnd.ReturnReferenceMethod("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<string, string>("Hello world", 42));
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w2TEnd.ReturnGenericMethod<int, int>(42, 99));
        Console.WriteLine();
    }
}

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
class With2ArgumentsThrowOnBegin : With2Arguments { }
class With2ArgumentsThrowOnEnd : With2Arguments { }

partial class ArgumentsParentType
{
    public class With2Arguments
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg, int arg21) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsInherits : With2Arguments { }
    public class With2ArgumentsInheritsGeneric : With2ArgumentsGeneric<int> { }
    public struct With2ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public static class With2ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2) { }
        public static int ReturnValueMethod(string arg1, int arg2) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsThrowOnBegin : With2Arguments { }
    public class With2ArgumentsThrowOnEnd : With2Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With2Arguments
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg, int arg21) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsInherits : With2Arguments { }
    public class With2ArgumentsInheritsGeneric : With2ArgumentsGeneric<int> { }
    public struct With2ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public static class With2ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2) { }
        public static int ReturnValueMethod(string arg1, int arg2) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsThrowOnBegin : With2Arguments { }
    public class With2ArgumentsThrowOnEnd : With2Arguments { }
}

partial class ArgumentsGenericParentType<PType>
{
    public class With2Arguments
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg, int arg21) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsInherits : With2Arguments { }
    public class With2ArgumentsInheritsGeneric : With2ArgumentsGeneric<int> { }
    public struct With2ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public static class With2ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2) { }
        public static int ReturnValueMethod(string arg1, int arg2) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    public class With2ArgumentsThrowOnBegin : With2Arguments { }
    public class With2ArgumentsThrowOnEnd : With2Arguments { }
}
