using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument8()
    {
        var w8 = new With8Arguments();
        Console.WriteLine($"{typeof(With8Arguments).FullName}.VoidMethod");
        RunMethod(() => w8.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w8.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8g1 = new With8ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly());
        RunMethod(() => w8g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w8g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w8g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8g2 = new With8ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w8g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w8g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w8g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8in = new With8ArgumentsInherits();
        Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w8in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w8in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8inGen = new With8ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w8inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w8inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w8inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8Struct = new With8ArgumentsStruct();
        Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w8Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w8Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With8ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With8ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With8ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With8ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With8ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8TBegin = new With8ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w8TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w8TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8TEnd = new With8ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w8TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w8TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
    }

    private static void ParentArgument8()
    {
        var w8 = new ArgumentsParentType.With8Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8Arguments).FullName}.VoidMethod");
        RunMethod(() => w8.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w8.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8g1 = new ArgumentsParentType.With8ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly());
        RunMethod(() => w8g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w8g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w8g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8g2 = new ArgumentsParentType.With8ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w8g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w8g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w8g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8in = new ArgumentsParentType.With8ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w8in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w8in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8inGen = new ArgumentsParentType.With8ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w8inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w8inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w8inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8Struct = new ArgumentsParentType.With8ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w8Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w8Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With8ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With8ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With8ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With8ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With8ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8TBegin = new ArgumentsParentType.With8ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w8TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w8TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8TEnd = new ArgumentsParentType.With8ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w8TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w8TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
    }

    private static void StructParentArgument8()
    {
        var w8 = new ArgumentsStructParentType.With8Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8Arguments).FullName}.VoidMethod");
        RunMethod(() => w8.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w8.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8g1 = new ArgumentsStructParentType.With8ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly());
        RunMethod(() => w8g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w8g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w8g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8g2 = new ArgumentsStructParentType.With8ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w8g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w8g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w8g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8in = new ArgumentsStructParentType.With8ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w8in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w8in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8inGen = new ArgumentsStructParentType.With8ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w8inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w8inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w8inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8Struct = new ArgumentsStructParentType.With8ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w8Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w8Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With8ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With8ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With8ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With8ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With8ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8TBegin = new ArgumentsStructParentType.With8ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w8TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w8TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
        //
        var w8TEnd = new ArgumentsStructParentType.With8ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w8TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w8TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w8TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w8TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w8TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
        Console.WriteLine();
    }
}

class With8Arguments
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
    public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
}
class With8ArgumentsGeneric<T>
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
    public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
}
class With8ArgumentsInherits : With8Arguments { }
class With8ArgumentsInheritsGeneric : With8ArgumentsGeneric<int> { }
struct With8ArgumentsStruct
{
    public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
    public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
    public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
    public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
}
static class With8ArgumentsStatic
{
    public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
    public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
    public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
    public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
}
class With8ArgumentsThrowOnBegin : With8Arguments { }
class With8ArgumentsThrowOnEnd : With8Arguments { }

partial class ArgumentsParentType
{
    public class With8Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public class With8ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public class With8ArgumentsInherits : With8Arguments { }
    public class With8ArgumentsInheritsGeneric : With8ArgumentsGeneric<int> { }
    public struct With8ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public static class With8ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public class With8ArgumentsThrowOnBegin : With8Arguments { }
    public class With8ArgumentsThrowOnEnd : With8Arguments { }
}

partial struct ArgumentsStructParentType
{
    public class With8Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public class With8ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public class With8ArgumentsInherits : With8Arguments { }
    public class With8ArgumentsInheritsGeneric : With8ArgumentsGeneric<int> { }
    public struct With8ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public static class With8ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7, Assembly arg8) => default;
    }
    public class With8ArgumentsThrowOnBegin : With8Arguments { }
    public class With8ArgumentsThrowOnEnd : With8Arguments { }
}
