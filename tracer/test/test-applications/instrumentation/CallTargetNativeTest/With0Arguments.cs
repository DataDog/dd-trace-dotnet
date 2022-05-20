using System;
using System.Threading.Tasks;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Argument0()
    {
        var w0 = new With0Arguments();
        Console.WriteLine($"{typeof(With0Arguments).FullName}.VoidMethod");
        RunMethod(() => w0.VoidMethod());
        Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w0.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0g1 = new With0ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w0g1.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g1.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g1.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g1.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0g2 = new With0ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w0g2.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g2.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g2.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g2.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0in = new With0ArgumentsInherits();
        Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w0in.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w0in.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0in.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0in.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0in.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0inGen = new With0ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w0inGen.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w0inGen.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0inGen.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w0inGen.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0Struct = new With0ArgumentsStruct();
        Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w0Struct.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w0Struct.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0Struct.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0Struct.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0Struct.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => With0ArgumentsStatic.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => With0ArgumentsStatic.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => With0ArgumentsStatic.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => With0ArgumentsStatic.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => With0ArgumentsStatic.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TBegin = new With0ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w0TBegin.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w0TBegin.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TBegin.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TEnd = new With0ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w0TEnd.VoidMethod());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w0TEnd.ReturnValueMethod());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TEnd.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TAsyncEnd = new With0ArgumentsThrowOnAsyncEnd();
        Console.WriteLine($"{typeof(With0ArgumentsThrowOnAsyncEnd).FullName}.Wait2Seconds");
        RunMethod(() => w0TAsyncEnd.Wait2Seconds().Wait());
        Console.WriteLine();
    }

    private static void ParentArgument0()
    {
        var w0 = new ArgumentsParentType.With0Arguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0Arguments).FullName}.VoidMethod");
        RunMethod(() => w0.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w0.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0g1 = new ArgumentsParentType.With0ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w0g1.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g1.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g1.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g1.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0g2 = new ArgumentsParentType.With0ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w0g2.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g2.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g2.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g2.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0in = new ArgumentsParentType.With0ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w0in.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w0in.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0in.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0in.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0in.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0inGen = new ArgumentsParentType.With0ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w0inGen.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w0inGen.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0inGen.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w0inGen.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0Struct = new ArgumentsParentType.With0ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w0Struct.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w0Struct.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0Struct.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0Struct.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0Struct.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsParentType.With0ArgumentsStatic.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsParentType.With0ArgumentsStatic.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsParentType.With0ArgumentsStatic.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsParentType.With0ArgumentsStatic.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsParentType.With0ArgumentsStatic.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TBegin = new ArgumentsParentType.With0ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w0TBegin.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w0TBegin.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TBegin.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TEnd = new ArgumentsParentType.With0ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w0TEnd.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w0TEnd.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TEnd.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TAsyncEnd = new ArgumentsParentType.With0ArgumentsThrowOnAsyncEnd();
        Console.WriteLine($"{typeof(ArgumentsParentType.With0ArgumentsThrowOnAsyncEnd).FullName}.Wait2Seconds");
        RunMethod(() => w0TAsyncEnd.Wait2Seconds().Wait());
        Console.WriteLine();
    }

    private static void StructParentArgument0()
    {
        var w0 = new ArgumentsStructParentType.With0Arguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0Arguments).FullName}.VoidMethod");
        RunMethod(() => w0.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w0.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0g1 = new ArgumentsStructParentType.With0ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w0g1.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g1.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g1.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g1.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0g2 = new ArgumentsStructParentType.With0ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w0g2.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g2.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g2.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g2.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0in = new ArgumentsStructParentType.With0ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w0in.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w0in.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0in.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0in.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0in.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0inGen = new ArgumentsStructParentType.With0ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w0inGen.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w0inGen.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0inGen.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w0inGen.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0Struct = new ArgumentsStructParentType.With0ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w0Struct.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w0Struct.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0Struct.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0Struct.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0Struct.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsStructParentType.With0ArgumentsStatic.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsStructParentType.With0ArgumentsStatic.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsStructParentType.With0ArgumentsStatic.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsStructParentType.With0ArgumentsStatic.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsStructParentType.With0ArgumentsStatic.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TBegin = new ArgumentsStructParentType.With0ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w0TBegin.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w0TBegin.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TBegin.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TEnd = new ArgumentsStructParentType.With0ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w0TEnd.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w0TEnd.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TEnd.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TAsyncEnd = new ArgumentsStructParentType.With0ArgumentsThrowOnAsyncEnd();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.With0ArgumentsThrowOnAsyncEnd).FullName}.Wait2Seconds");
        RunMethod(() => w0TAsyncEnd.Wait2Seconds().Wait());
        Console.WriteLine();
    }

    private static void GenericParentArgument0()
    {
        var w0 = new ArgumentsGenericParentType<object>.With0Arguments();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0Arguments).FullName}.VoidMethod");
        RunMethod(() => w0.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0Arguments).FullName}.ReturnValueMethod");
        RunMethod(() => w0.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0Arguments).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0Arguments).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0Arguments).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0g1 = new ArgumentsGenericParentType<object>.With0ArgumentsGeneric<string>();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<string>).FullName}.VoidMethod");
        RunMethod(() => w0g1.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g1.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g1.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g1.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0g2 = new ArgumentsGenericParentType<object>.With0ArgumentsGeneric<int>();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<int>).FullName}.VoidMethod");
        RunMethod(() => w0g2.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
        RunMethod(() => w0g2.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0g2.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
        RunMethod(() => w0g2.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0in = new ArgumentsGenericParentType<object>.With0ArgumentsInherits();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInherits).FullName}.VoidMethod");
        RunMethod(() => w0in.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInherits).FullName}.ReturnValueMethod");
        RunMethod(() => w0in.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInherits).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0in.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0in.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0in.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0inGen = new ArgumentsGenericParentType<object>.With0ArgumentsInheritsGeneric();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInheritsGeneric).FullName}.VoidMethod");
        RunMethod(() => w0inGen.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
        RunMethod(() => w0inGen.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0inGen.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
        RunMethod(() => w0inGen.ReturnGenericMethod());
        Console.WriteLine();
        //
        var w0Struct = new ArgumentsGenericParentType<object>.With0ArgumentsStruct();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStruct).FullName}.VoidMethod");
        RunMethod(() => w0Struct.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStruct).FullName}.ReturnValueMethod");
        RunMethod(() => w0Struct.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStruct).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0Struct.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0Struct.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0Struct.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStatic).FullName}.VoidMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With0ArgumentsStatic.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStatic).FullName}.ReturnValueMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With0ArgumentsStatic.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStatic).FullName}.ReturnReferenceMethod");
        RunMethod(() => ArgumentsGenericParentType<object>.With0ArgumentsStatic.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => ArgumentsGenericParentType<object>.With0ArgumentsStatic.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => ArgumentsGenericParentType<object>.With0ArgumentsStatic.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TBegin = new ArgumentsGenericParentType<object>.With0ArgumentsThrowOnBegin();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnBegin).FullName}.VoidMethod");
        RunMethod(() => w0TBegin.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
        RunMethod(() => w0TBegin.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TBegin.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TBegin.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TEnd = new ArgumentsGenericParentType<object>.With0ArgumentsThrowOnEnd();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnEnd).FullName}.VoidMethod");
        RunMethod(() => w0TEnd.VoidMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
        RunMethod(() => w0TEnd.ReturnValueMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
        RunMethod(() => w0TEnd.ReturnReferenceMethod());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<string>());
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
        RunMethod(() => w0TEnd.ReturnGenericMethod<int>());
        Console.WriteLine();
        //
        var w0TAsyncEnd = new ArgumentsGenericParentType<object>.With0ArgumentsThrowOnAsyncEnd();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.With0ArgumentsThrowOnAsyncEnd).FullName}.Wait2Seconds");
        RunMethod(() => w0TAsyncEnd.Wait2Seconds().Wait());
        Console.WriteLine();
    }
}

class With0Arguments
{
    public void VoidMethod() { }
    public int ReturnValueMethod() => 42;
    public string ReturnReferenceMethod() => "Hello World";
    public T ReturnGenericMethod<T>() => default;
}
class With0ArgumentsGeneric<T>
{
    public void VoidMethod() { }
    public int ReturnValueMethod() => 42;
    public string ReturnReferenceMethod() => "Hello World";
    public T ReturnGenericMethod() => default;
}
class With0ArgumentsInherits : With0Arguments { }
class With0ArgumentsInheritsGeneric : With0ArgumentsGeneric<int> { }
struct With0ArgumentsStruct
{
    public void VoidMethod() { }
    public int ReturnValueMethod() => 42;
    public string ReturnReferenceMethod() => "Hello World";
    public T ReturnGenericMethod<T>() => default;
}
static class With0ArgumentsStatic
{
    public static void VoidMethod() { }
    public static int ReturnValueMethod() => 42;
    public static string ReturnReferenceMethod() => "Hello World";
    public static T ReturnGenericMethod<T>() => default;
}
class With0ArgumentsThrowOnBegin : With0Arguments { }
class With0ArgumentsThrowOnEnd : With0Arguments { }
class With0ArgumentsThrowOnAsyncEnd
{
    public Task Wait2Seconds() => Task.Delay(2000);
}

partial class ArgumentsParentType
{
    // *** With0Arguments
    public class With0Arguments
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    public class With0ArgumentsGeneric<T>
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod() => default;
    }
    public class With0ArgumentsInherits : With0Arguments { }
    public class With0ArgumentsInheritsGeneric : With0ArgumentsGeneric<int> { }
    public struct With0ArgumentsStruct
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    public static class With0ArgumentsStatic
    {
        public static void VoidMethod() { }
        public static int ReturnValueMethod() => 42;
        public static string ReturnReferenceMethod() => "Hello World";
        public static T ReturnGenericMethod<T>() => default;
    }
    public class With0ArgumentsThrowOnBegin : With0Arguments { }
    public class With0ArgumentsThrowOnEnd : With0Arguments { }
    public class With0ArgumentsThrowOnAsyncEnd
    {
        public Task Wait2Seconds() => Task.Delay(2000);
    }
}

partial struct ArgumentsStructParentType
{
    // *** With0Arguments
    public class With0Arguments
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    public class With0ArgumentsGeneric<T>
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod() => default;
    }
    public class With0ArgumentsInherits : With0Arguments { }
    public class With0ArgumentsInheritsGeneric : With0ArgumentsGeneric<int> { }
    public struct With0ArgumentsStruct
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    public static class With0ArgumentsStatic
    {
        public static void VoidMethod() { }
        public static int ReturnValueMethod() => 42;
        public static string ReturnReferenceMethod() => "Hello World";
        public static T ReturnGenericMethod<T>() => default;
    }
    public class With0ArgumentsThrowOnBegin : With0Arguments { }
    public class With0ArgumentsThrowOnEnd : With0Arguments { }
    public class With0ArgumentsThrowOnAsyncEnd
    {
        public Task Wait2Seconds() => Task.Delay(2000);
    }
}

partial class ArgumentsGenericParentType<PType>
{
    // *** With0Arguments
    public class With0Arguments
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    public class With0ArgumentsGeneric<T>
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod() => default;
    }
    public class With0ArgumentsInherits : With0Arguments { }
    public class With0ArgumentsInheritsGeneric : With0ArgumentsGeneric<int> { }
    public struct With0ArgumentsStruct
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    public static class With0ArgumentsStatic
    {
        public static void VoidMethod() { }
        public static int ReturnValueMethod() => 42;
        public static string ReturnReferenceMethod() => "Hello World";
        public static T ReturnGenericMethod<T>() => default;
    }
    public class With0ArgumentsThrowOnBegin : With0Arguments { }
    public class With0ArgumentsThrowOnEnd : With0Arguments { }
    public class With0ArgumentsThrowOnAsyncEnd
    {
        public Task Wait2Seconds() => Task.Delay(2000);
    }
}
