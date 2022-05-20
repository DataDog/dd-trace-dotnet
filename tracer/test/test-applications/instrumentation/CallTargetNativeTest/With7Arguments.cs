using System;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument7()
    {
        var w7 = new With7Arguments();
        Console.WriteLine($"{typeof(With7Arguments).FullName}.VoidMethod");
        RunMethod(() => w7.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w7.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7g1 = new With7ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w7g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w7g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w7g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7g2 = new With7ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w7g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w7g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w7g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7in = new With7ArgumentsInherits();
        Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w7in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w7in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7inGen = new With7ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w7inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w7inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w7inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7Struct = new With7ArgumentsStruct();
        Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w7Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w7Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With7ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With7ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With7ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With7ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With7ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7TBegin = new With7ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w7TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w7TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7TEnd = new With7ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w7TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w7TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
    }

    private static void ParentArgument7()
    {
        var w7 = new ArgumentsParentType.With7Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7Arguments).FullName}.VoidMethod");
        RunMethod(() => w7.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w7.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7g1 = new ArgumentsParentType.With7ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w7g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w7g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w7g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7g2 = new ArgumentsParentType.With7ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w7g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w7g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w7g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7in = new ArgumentsParentType.With7ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w7in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w7in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7inGen = new ArgumentsParentType.With7ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w7inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w7inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w7inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7Struct = new ArgumentsParentType.With7ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w7Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w7Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With7ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With7ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With7ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With7ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With7ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7TBegin = new ArgumentsParentType.With7ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w7TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w7TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7TEnd = new ArgumentsParentType.With7ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w7TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w7TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
    }

    private static void StructParentArgument7()
    {
        var w7 = new ArgumentsStructParentType.With7Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7Arguments).FullName}.VoidMethod");
        RunMethod(() => w7.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w7.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7g1 = new ArgumentsStructParentType.With7ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w7g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w7g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w7g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7g2 = new ArgumentsStructParentType.With7ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w7g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w7g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w7g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7in = new ArgumentsStructParentType.With7ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w7in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w7in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7inGen = new ArgumentsStructParentType.With7ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w7inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w7inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w7inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7Struct = new ArgumentsStructParentType.With7ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w7Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w7Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With7ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With7ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With7ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With7ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With7ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7TBegin = new ArgumentsStructParentType.With7ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w7TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w7TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
        //
        var w7TEnd = new ArgumentsStructParentType.With7ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w7TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w7TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w7TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w7TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w7TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
        Console.WriteLine();
    }
}

class With7Arguments
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
    public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
}
class With7ArgumentsGeneric<T>
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
    public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
}
class With7ArgumentsInherits : With7Arguments { }
class With7ArgumentsInheritsGeneric : With7ArgumentsGeneric<int> { }
struct With7ArgumentsStruct
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
}
static class With7ArgumentsStatic
{
    public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
    public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
    public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
    public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
}
class With7ArgumentsThrowOnBegin : With7Arguments { }
class With7ArgumentsThrowOnEnd : With7Arguments { }

partial class ArgumentsParentType
{
    public class With7Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public class With7ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public class With7ArgumentsInherits : With7Arguments { }
    public class With7ArgumentsInheritsGeneric : With7ArgumentsGeneric<int> { }
    public struct With7ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public static class With7ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public class With7ArgumentsThrowOnBegin : With7Arguments { }
    public class With7ArgumentsThrowOnEnd : With7Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With7Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public class With7ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public class With7ArgumentsInherits : With7Arguments { }
    public class With7ArgumentsInheritsGeneric : With7ArgumentsGeneric<int> { }
    public struct With7ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public static class With7ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    public class With7ArgumentsThrowOnBegin : With7Arguments { }
    public class With7ArgumentsThrowOnEnd : With7Arguments { }
}
