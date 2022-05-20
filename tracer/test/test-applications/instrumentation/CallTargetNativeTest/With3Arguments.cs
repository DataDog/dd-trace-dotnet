using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument3()
    {
        var w3 = new With3Arguments();
        Console.WriteLine($"{typeof(With3Arguments).FullName}.VoidMethod");
        RunMethod(() => w3.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w3.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3g1 = new With3ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w3g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w3g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w3g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3g2 = new With3ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w3g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w3g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w3g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3in = new With3ArgumentsInherits();
        Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w3in.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w3in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3inGen = new With3ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w3inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w3inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w3inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3Struct = new With3ArgumentsStruct();
        Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w3Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w3Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With3ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With3ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With3ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With3ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With3ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3TBegin = new With3ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w3TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w3TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3TEnd = new With3ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w3TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w3TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
    }

    private static void ParentArgument3()
    {
        var w3 = new ArgumentsParentType.With3Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3Arguments).FullName}.VoidMethod");
        RunMethod(() => w3.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w3.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3g1 = new ArgumentsParentType.With3ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w3g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w3g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w3g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3g2 = new ArgumentsParentType.With3ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w3g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w3g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w3g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3in = new ArgumentsParentType.With3ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w3in.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w3in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3inGen = new ArgumentsParentType.With3ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w3inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w3inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w3inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3Struct = new ArgumentsParentType.With3ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w3Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w3Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With3ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With3ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With3ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With3ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With3ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3TBegin = new ArgumentsParentType.With3ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w3TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w3TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3TEnd = new ArgumentsParentType.With3ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w3TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w3TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
    }

    private static void StructParentArgument3()
    {
        var w3 = new ArgumentsStructParentType.With3Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3Arguments).FullName}.VoidMethod");
        RunMethod(() => w3.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w3.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3g1 = new ArgumentsStructParentType.With3ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w3g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w3g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w3g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3g2 = new ArgumentsStructParentType.With3ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w3g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w3g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w3g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3in = new ArgumentsStructParentType.With3ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w3in.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w3in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3inGen = new ArgumentsStructParentType.With3ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w3inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w3inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w3inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3Struct = new ArgumentsStructParentType.With3ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w3Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w3Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With3ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With3ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With3ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With3ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With3ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3TBegin = new ArgumentsStructParentType.With3ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w3TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w3TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
        //
        var w3TEnd = new ArgumentsStructParentType.With3ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w3TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w3TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w3TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w3TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w3TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
        Console.WriteLine();
    }
}

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
class With3ArgumentsThrowOnBegin : With3Arguments { }
class With3ArgumentsThrowOnEnd : With3Arguments { }

partial class ArgumentsParentType
{
    public class With3Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public class With3ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public class With3ArgumentsInherits : With3Arguments { }
    public class With3ArgumentsInheritsGeneric : With3ArgumentsGeneric<int> { }
    public struct With3ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public static class With3ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public class With3ArgumentsThrowOnBegin : With3Arguments { }
    public class With3ArgumentsThrowOnEnd : With3Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With3Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public class With3ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public class With3ArgumentsInherits : With3Arguments { }
    public class With3ArgumentsInheritsGeneric : With3ArgumentsGeneric<int> { }
    public struct With3ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public static class With3ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    public class With3ArgumentsThrowOnBegin : With3Arguments { }
    public class With3ArgumentsThrowOnEnd : With3Arguments { }
}
