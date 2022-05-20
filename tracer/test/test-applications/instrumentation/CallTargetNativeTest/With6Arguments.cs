using System;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument6()
    {
        var w6 = new With6Arguments();
        Console.WriteLine($"{typeof(With6Arguments).FullName}.VoidMethod");
        RunMethod(() => w6.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w6.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6.ReturnGenericMethod<Task<int>, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6g1 = new With6ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w6g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w6g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w6g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6g2 = new With6ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w6g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w6g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w6g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6in = new With6ArgumentsInherits();
        Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w6in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w6in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6inGen = new With6ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w6inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w6inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w6inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6Struct = new With6ArgumentsStruct();
        Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w6Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w6Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With6ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With6ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With6ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With6ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With6ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6TBegin = new With6ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w6TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w6TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6TEnd = new With6ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w6TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w6TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
    }

    private static void ParentArgument6()
    {
        var w6 = new ArgumentsParentType.With6Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6Arguments).FullName}.VoidMethod");
        RunMethod(() => w6.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w6.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6.ReturnGenericMethod<Task<int>, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6g1 = new ArgumentsParentType.With6ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w6g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w6g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w6g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6g2 = new ArgumentsParentType.With6ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w6g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w6g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w6g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6in = new ArgumentsParentType.With6ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w6in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w6in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6inGen = new ArgumentsParentType.With6ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w6inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w6inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w6inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6Struct = new ArgumentsParentType.With6ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w6Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w6Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With6ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With6ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With6ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With6ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With6ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6TBegin = new ArgumentsParentType.With6ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w6TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w6TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6TEnd = new ArgumentsParentType.With6ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w6TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w6TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
    }

    private static void StructParentArgument6()
    {
        var w6 = new ArgumentsStructParentType.With6Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6Arguments).FullName}.VoidMethod");
        RunMethod(() => w6.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w6.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6.ReturnGenericMethod<Task<int>, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6g1 = new ArgumentsStructParentType.With6ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w6g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w6g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w6g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6g2 = new ArgumentsStructParentType.With6ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w6g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w6g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w6g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6in = new ArgumentsStructParentType.With6ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w6in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w6in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6inGen = new ArgumentsStructParentType.With6ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w6inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w6inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w6inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6Struct = new ArgumentsStructParentType.With6ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w6Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w6Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With6ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With6ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With6ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With6ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With6ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6TBegin = new ArgumentsStructParentType.With6ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w6TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w6TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
        //
        var w6TEnd = new ArgumentsStructParentType.With6ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w6TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w6TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w6TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w6TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w6TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
        Console.WriteLine();
    }
}

class With6Arguments
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
    public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
}
class With6ArgumentsGeneric<T>
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
    public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
}
class With6ArgumentsInherits : With6Arguments { }
class With6ArgumentsInheritsGeneric : With6ArgumentsGeneric<int> { }
struct With6ArgumentsStruct
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
}
static class With6ArgumentsStatic
{
    public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
    public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
    public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
    public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
}
class With6ArgumentsThrowOnBegin : With6Arguments { }
class With6ArgumentsThrowOnEnd : With6Arguments { }

partial class ArgumentsParentType
{
    public class With6Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public class With6ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public class With6ArgumentsInherits : With6Arguments { }
    public class With6ArgumentsInheritsGeneric : With6ArgumentsGeneric<int> { }
    public struct With6ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public static class With6ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public class With6ArgumentsThrowOnBegin : With6Arguments { }
    public class With6ArgumentsThrowOnEnd : With6Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With6Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public class With6ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public class With6ArgumentsInherits : With6Arguments { }
    public class With6ArgumentsInheritsGeneric : With6ArgumentsGeneric<int> { }
    public struct With6ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public static class With6ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    public class With6ArgumentsThrowOnBegin : With6Arguments { }
    public class With6ArgumentsThrowOnEnd : With6Arguments { }
}
