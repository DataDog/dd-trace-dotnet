using System;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument5()
    {
        var w5 = new With5Arguments();
        Console.WriteLine($"{typeof(With5Arguments).FullName}.VoidMethod");
        RunMethod(() => w5.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w5.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5g1 = new With5ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w5g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w5g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w5g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5g2 = new With5ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w5g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w5g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w5g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5in = new With5ArgumentsInherits();
        Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w5in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w5in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5inGen = new With5ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w5inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w5inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w5inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5Struct = new With5ArgumentsStruct();
        Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w5Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w5Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With5ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With5ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With5ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With5ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With5ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5TBegin = new With5ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w5TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w5TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5TEnd = new With5ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w5TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w5TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
    }

    private static void ParentArgument5()
    {
        var w5 = new ArgumentsParentType.With5Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5Arguments).FullName}.VoidMethod");
        RunMethod(() => w5.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w5.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5g1 = new ArgumentsParentType.With5ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w5g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w5g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w5g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5g2 = new ArgumentsParentType.With5ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w5g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w5g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w5g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5in = new ArgumentsParentType.With5ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w5in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w5in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5inGen = new ArgumentsParentType.With5ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w5inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w5inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w5inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5Struct = new ArgumentsParentType.With5ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w5Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w5Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With5ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With5ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With5ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With5ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With5ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5TBegin = new ArgumentsParentType.With5ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w5TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w5TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5TEnd = new ArgumentsParentType.With5ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w5TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w5TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
    }

    private static void StructParentArgument5()
    {
        var w5 = new ArgumentsStructParentType.With5Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5Arguments).FullName}.VoidMethod");
        RunMethod(() => w5.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w5.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5g1 = new ArgumentsStructParentType.With5ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w5g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w5g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w5g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5g2 = new ArgumentsStructParentType.With5ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w5g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w5g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w5g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5in = new ArgumentsStructParentType.With5ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w5in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w5in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5inGen = new ArgumentsStructParentType.With5ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w5inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w5inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w5inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5Struct = new ArgumentsStructParentType.With5ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w5Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w5Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With5ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With5ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With5ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With5ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With5ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5TBegin = new ArgumentsStructParentType.With5ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w5TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w5TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
        //
        var w5TEnd = new ArgumentsStructParentType.With5ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w5TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w5TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w5TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w5TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w5TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
        Console.WriteLine();
    }
}

class With5Arguments
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
    public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
}
class With5ArgumentsGeneric<T>
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
    public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
}
class With5ArgumentsInherits : With5Arguments { }
class With5ArgumentsInheritsGeneric : With5ArgumentsGeneric<int> { }
struct With5ArgumentsStruct
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
}
static class With5ArgumentsStatic
{
    public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
    public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
    public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
    public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
}
class With5ArgumentsThrowOnBegin : With5Arguments { }
class With5ArgumentsThrowOnEnd : With5Arguments { }

partial class ArgumentsParentType
{
    public class With5Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public class With5ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public class With5ArgumentsInherits : With5Arguments { }
    public class With5ArgumentsInheritsGeneric : With5ArgumentsGeneric<int> { }
    public struct With5ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public static class With5ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public class With5ArgumentsThrowOnBegin : With5Arguments { }
    public class With5ArgumentsThrowOnEnd : With5Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With5Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public class With5ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public class With5ArgumentsInherits : With5Arguments { }
    public class With5ArgumentsInheritsGeneric : With5ArgumentsGeneric<int> { }
    public struct With5ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public static class With5ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    public class With5ArgumentsThrowOnBegin : With5Arguments { }
    public class With5ArgumentsThrowOnEnd : With5Arguments { }
}
