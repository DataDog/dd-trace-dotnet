using System;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument4()
    {
        var w4 = new With4Arguments();
        Console.WriteLine($"{typeof(With4Arguments).FullName}.VoidMethod");
        RunMethod(() => w4.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w4.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4g1 = new With4ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w4g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w4g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w4g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4g2 = new With4ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w4g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w4g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w4g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4in = new With4ArgumentsInherits();
        Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w4in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w4in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4inGen = new With4ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w4inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w4inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w4inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4Struct = new With4ArgumentsStruct();
        Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w4Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w4Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With4ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With4ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With4ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With4ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With4ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4TBegin = new With4ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w4TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w4TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4TEnd = new With4ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w4TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w4TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
    }

    private static void ParentArgument4()
    {
        var w4 = new ArgumentsParentType.With4Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4Arguments).FullName}.VoidMethod");
        RunMethod(() => w4.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w4.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4g1 = new ArgumentsParentType.With4ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w4g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w4g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w4g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4g2 = new ArgumentsParentType.With4ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w4g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w4g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w4g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4in = new ArgumentsParentType.With4ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w4in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w4in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4inGen = new ArgumentsParentType.With4ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w4inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w4inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w4inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4Struct = new ArgumentsParentType.With4ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w4Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w4Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With4ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With4ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With4ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With4ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With4ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4TBegin = new ArgumentsParentType.With4ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w4TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w4TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4TEnd = new ArgumentsParentType.With4ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w4TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w4TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
    }

    private static void StructParentArgument4()
    {
        var w4 = new ArgumentsStructParentType.With4Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4Arguments).FullName}.VoidMethod");
        RunMethod(() => w4.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w4.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4g1 = new ArgumentsStructParentType.With4ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w4g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w4g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w4g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4g2 = new ArgumentsStructParentType.With4ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w4g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w4g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w4g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4in = new ArgumentsStructParentType.With4ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w4in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w4in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4inGen = new ArgumentsStructParentType.With4ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w4inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w4inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w4inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4Struct = new ArgumentsStructParentType.With4ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w4Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w4Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With4ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With4ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With4ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With4ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With4ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4TBegin = new ArgumentsStructParentType.With4ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w4TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w4TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
        //
        var w4TEnd = new ArgumentsStructParentType.With4ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w4TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w4TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w4TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w4TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w4TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
        Console.WriteLine();
    }
}

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

partial class ArgumentsParentType
{
    public class With4Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public class With4ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public class With4ArgumentsInherits : With4Arguments { }
    public class With4ArgumentsInheritsGeneric : With4ArgumentsGeneric<int> { }
    public struct With4ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public static class With4ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public class With4ArgumentsThrowOnBegin : With4Arguments { }
    public class With4ArgumentsThrowOnEnd : With4Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With4Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public class With4ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public class With4ArgumentsInherits : With4Arguments { }
    public class With4ArgumentsInheritsGeneric : With4ArgumentsGeneric<int> { }
    public struct With4ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public static class With4ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    public class With4ArgumentsThrowOnBegin : With4Arguments { }
    public class With4ArgumentsThrowOnEnd : With4Arguments { }
}
