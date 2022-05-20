using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument9()
    {
        var w9 = new With9Arguments();
        Console.WriteLine($"{typeof(With9Arguments).FullName}.VoidMethod");
        RunMethod(() => w9.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w9.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9g1 = new With9ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly(), null);
        RunMethod(() => w9g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w9g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w9g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9g2 = new With9ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w9g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w9g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w9g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9in = new With9ArgumentsInherits();
        Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w9in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w9in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9inGen = new With9ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w9inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w9inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w9inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9Struct = new With9ArgumentsStruct();
        Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w9Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w9Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With9ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With9ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With9ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With9ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With9ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9TBegin = new With9ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w9TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w9TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9TEnd = new With9ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w9TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w9TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
    }

    private static void ParentArgument9()
    {
        var w9 = new ArgumentsParentType.With9Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9Arguments).FullName}.VoidMethod");
        RunMethod(() => w9.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w9.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9g1 = new ArgumentsParentType.With9ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly(), null);
        RunMethod(() => w9g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w9g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w9g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9g2 = new ArgumentsParentType.With9ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w9g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w9g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w9g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9in = new ArgumentsParentType.With9ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w9in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w9in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9inGen = new ArgumentsParentType.With9ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w9inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w9inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w9inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9Struct = new ArgumentsParentType.With9ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w9Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w9Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With9ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With9ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With9ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With9ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With9ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9TBegin = new ArgumentsParentType.With9ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w9TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w9TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9TEnd = new ArgumentsParentType.With9ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w9TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w9TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
    }

    private static void StructParentArgument9()
    {
        var w9 = new ArgumentsStructParentType.With9Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9Arguments).FullName}.VoidMethod");
        RunMethod(() => w9.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w9.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9g1 = new ArgumentsStructParentType.With9ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly(), null);
        RunMethod(() => w9g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w9g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w9g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9g2 = new ArgumentsStructParentType.With9ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w9g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w9g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w9g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9in = new ArgumentsStructParentType.With9ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w9in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w9in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9inGen = new ArgumentsStructParentType.With9ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w9inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w9inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w9inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9Struct = new ArgumentsStructParentType.With9ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w9Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w9Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With9ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With9ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With9ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With9ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With9ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9TBegin = new ArgumentsStructParentType.With9ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w9TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w9TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
        //
        var w9TEnd = new ArgumentsStructParentType.With9ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w9TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w9TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w9TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w9TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w9TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        Console.WriteLine();
    }
}

class With9Arguments
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
    public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
}
class With9ArgumentsGeneric<T>
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
    public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
}
class With9ArgumentsInherits : With9Arguments { }
class With9ArgumentsInheritsGeneric : With9ArgumentsGeneric<int> { }
struct With9ArgumentsStruct
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
}
static class With9ArgumentsStatic
{
    public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
    public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
    public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
    public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
}
class With9ArgumentsThrowOnBegin : With9Arguments { }
class With9ArgumentsThrowOnEnd : With9Arguments { }

partial class ArgumentsParentType
{
    public class With9Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public class With9ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public class With9ArgumentsInherits : With9Arguments { }
    public class With9ArgumentsInheritsGeneric : With9ArgumentsGeneric<int> { }
    public struct With9ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public static class With9ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public class With9ArgumentsThrowOnBegin : With9Arguments { }
    public class With9ArgumentsThrowOnEnd : With9Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With9Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public class With9ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public class With9ArgumentsInherits : With9Arguments { }
    public class With9ArgumentsInheritsGeneric : With9ArgumentsGeneric<int> { }
    public struct With9ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public static class With9ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8, int? arg9) => default;
    }
    public class With9ArgumentsThrowOnBegin : With9Arguments { }
    public class With9ArgumentsThrowOnEnd : With9Arguments { }
}
