using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void WithRefStructArguments()
    {
        var wRefStructArg = new WithRefStructArguments();

        Console.WriteLine($"{typeof(WithRefArguments).FullName} | ReadOnlySpan<char> methods");
        RunMethod(() => wRefStructArg.VoidReadOnlySpanMethod("BadParam".AsSpan()));
        RunMethod(() =>
        {
            var arg1 = "BadParam".AsSpan();
            wRefStructArg.VoidReadOnlySpanMethod(ref arg1);
            if (!arg1.SequenceEqual("Hello World".AsSpan())) throw new Exception("Error modifying arg1 value.");
        });
        RunMethod(() =>
        {
            var arg1 = "BadParam".AsSpan();
            var arg2 = "BadParam".AsSpan();
            wRefStructArg.Void2ReadOnlySpanMethod(arg1, arg2);
        });
        RunMethod(() =>
        {
            var arg1 = "BadParam".AsSpan();
            var arg2 = "BadParam".AsSpan();
            wRefStructArg.Void2ReadOnlySpanMethod(ref arg1, ref arg2);
            if (!arg1.SequenceEqual("Hello".AsSpan())) throw new Exception("Error modifying arg1 value.");
            if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
        });
        RunMethod(() =>
        {
            var arg1 = "BadParam".AsSpan();
            var arg2 = "BadParam".AsSpan();
            wRefStructArg.Void2ReadOnlySpanMethod(arg1, ref arg2);
            if (!arg2.SequenceEqual("World".AsSpan()))
            {
                throw new Exception("Error modifying arg2 value.");
            }
        });
        
        Console.WriteLine($"{typeof(WithRefArguments).FullName} | Span<char> methods");
        RunMethod(() => wRefStructArg.VoidSpanMethod(new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm'])));
        RunMethod(() =>
        {
            char[] value = ['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm'];
            var original = new Span<char>(value);
            var arg1 = new Span<char>(value);
            wRefStructArg.VoidSpanMethod(ref arg1);
            if (arg1.ToString() != "Hello World") throw new Exception("Error modifying arg1 value.");
            if (original.ToString() != "BadParam") throw new Exception("Error: original value has not been kept.");
        });
        RunMethod(() =>
        {
            var arg1 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            var arg2 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            wRefStructArg.Void2SpanMethod(arg1, arg2);
        });
        RunMethod(() =>
        {
            var arg1 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            var arg2 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            wRefStructArg.Void2SpanMethod(ref arg1, ref arg2);
            if (arg1.ToString() != "Hello") throw new Exception("Error modifying arg1 value.");
            if (arg2.ToString() != "World") throw new Exception("Error modifying arg2 value.");
        });
        RunMethod(() =>
        {
            var arg1 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            var arg2 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            wRefStructArg.Void2SpanMethod(arg1, ref arg2);
            if (arg2.ToString() != "World") throw new Exception("Error modifying arg2 value.");
        });
        
        Console.WriteLine($"{typeof(WithRefArguments).FullName} | ReadOnlyRefStruct methods");
        RunMethod(() => wRefStructArg.VoidReadOnlyRefStructMethod(new ReadOnlyRefStruct("BadParam")));
        RunMethod(() =>
        {
            var arg1 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.VoidReadOnlyRefStructMethod(ref arg1);
            if (arg1.Value != "Hello World") throw new Exception("Error modifying arg1 value.");
        });
        RunMethod(() =>
        {
            var arg1 = new ReadOnlyRefStruct("BadParam");
            var arg2 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.Void2ReadOnlyRefStructMethod(arg1, arg2);
        });
        RunMethod(() =>
        {
            var arg1 = new ReadOnlyRefStruct("BadParam");
            var arg2 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.Void2ReadOnlyRefStructMethod(ref arg1, ref arg2);
            if (arg1.Value != "Hello") throw new Exception("Error modifying arg1 value.");
            if (arg2.Value != "World") throw new Exception("Error modifying arg2 value.");
        });
        RunMethod(() =>
        {
            var arg1 = new ReadOnlyRefStruct("BadParam");
            var arg2 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.Void2ReadOnlyRefStructMethod(arg1, ref arg2);
            if (arg2.Value != "World") throw new Exception("Error modifying arg2 value.");
        });
        
        Console.WriteLine($"{typeof(WithRefArguments).FullName} | VoidMixedMethod methods");
        RunMethod(() =>
        {
            var arg1 = "BadParam";
            var arg2 = "BadParam".AsSpan();
            var arg3 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            var arg4 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.VoidMixedMethod(arg1, arg2, arg3, arg4);
        });
        RunMethod(() =>
        {
            var arg1 = "BadParam";
            var arg2 = "BadParam".AsSpan();
            var arg3 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            var arg4 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.VoidMixedMethod(ref arg1, ref arg2, ref arg3, ref arg4);
            if (arg1 != "Hello") throw new Exception("Error modifying arg1 value.");
            if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
            if (arg3.ToString() != "Hello") throw new Exception("Error modifying arg3 value.");
            if (arg4.Value != "World") throw new Exception("Error modifying arg4 value.");
        });
        RunMethod(() =>
        {
            var arg1 = "BadParam";
            var arg2 = "BadParam".AsSpan();
            var arg3 = new Span<char>(['B', 'a', 'd', 'P', 'a', 'r', 'a', 'm']);
            var arg4 = new ReadOnlyRefStruct("BadParam");
            wRefStructArg.VoidMixedMethod(arg1, ref arg2, arg3, ref arg4);
            if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
            if (arg4.Value != "World") throw new Exception("Error modifying arg4 value.");
        });
    }
}

internal class WithRefStructArguments
{
    // *** ReadOnlySpan<char> arguments ***
    
    public void VoidReadOnlySpanMethod(ReadOnlySpan<char> arg1)
    {
        if (!arg1.SequenceEqual("Hello World".AsSpan())) throw new Exception("Error modifying arg1 value.");
    }

    public void VoidReadOnlySpanMethod(ref ReadOnlySpan<char> arg1)
    {
        if (!arg1.SequenceEqual("Hello World".AsSpan())) throw new Exception("Error modifying arg1 value.");
    }

    public void Void2ReadOnlySpanMethod(ReadOnlySpan<char> arg1, ReadOnlySpan<char> arg2)
    {
        if (!arg1.SequenceEqual("Hello".AsSpan())) throw new Exception("Error modifying arg1 value.");
        if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
    }

    public void Void2ReadOnlySpanMethod(ref ReadOnlySpan<char> arg1, ref ReadOnlySpan<char> arg2)
    {
        if (!arg1.SequenceEqual("Hello".AsSpan())) throw new Exception("Error modifying arg1 value.");
        if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
    }

    public void Void2ReadOnlySpanMethod(ReadOnlySpan<char> arg1, ref ReadOnlySpan<char> arg2)
    {
        if (!arg1.SequenceEqual("Hello".AsSpan())) throw new Exception("Error modifying arg1 value.");
        if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
    }
    
    // *** Span<char> arguments ***
    
    public void VoidSpanMethod(Span<char> arg1)
    {
        if (arg1.ToString() != "Hello World") throw new Exception("Error modifying arg1 value.");
    }

    public void VoidSpanMethod(ref Span<char> arg1)
    {
        if (arg1.ToString() != "Hello World") throw new Exception("Error modifying arg1 value.");
    }

    public void Void2SpanMethod(Span<char> arg1, Span<char> arg2)
    {
        if (arg1.ToString() != "Hello") throw new Exception("Error modifying arg1 value.");
        if (arg2.ToString() != "World") throw new Exception("Error modifying arg2 value.");
    }

    public void Void2SpanMethod(ref Span<char> arg1, ref Span<char> arg2)
    {
        if (arg1.ToString() != "Hello") throw new Exception("Error modifying arg1 value.");
        if (arg2.ToString() != "World") throw new Exception("Error modifying arg2 value.");
    }

    public void Void2SpanMethod(Span<char> arg1, ref Span<char> arg2)
    {
        if (arg1.ToString() != "Hello") throw new Exception("Error modifying arg1 value.");
        if (arg2.ToString() != "World") throw new Exception("Error modifying arg2 value.");
    }

    // *** ReadOnlyRefStruct arguments ***
    
    public void VoidReadOnlyRefStructMethod(ReadOnlyRefStruct arg1)
    {
        if (arg1.Value != "Hello World") throw new Exception("Error modifying arg1 value.");
    }

    public void VoidReadOnlyRefStructMethod(ref ReadOnlyRefStruct arg1)
    {
        if (arg1.Value != "Hello World") throw new Exception("Error modifying arg1 value.");
    }

    public void Void2ReadOnlyRefStructMethod(ReadOnlyRefStruct arg1, ReadOnlyRefStruct arg2)
    {
        if (arg1.Value != "Hello") throw new Exception("Error modifying arg1 value.");
        if (arg2.Value != "World") throw new Exception("Error modifying arg2 value.");
    }

    public void Void2ReadOnlyRefStructMethod(ref ReadOnlyRefStruct arg1, ref ReadOnlyRefStruct arg2)
    {
        if (arg1.Value != "Hello") throw new Exception("Error modifying arg1 value.");
        if (arg2.Value != "World") throw new Exception("Error modifying arg2 value.");
    }

    public void Void2ReadOnlyRefStructMethod(ReadOnlyRefStruct arg1, ref ReadOnlyRefStruct arg2)
    {
        if (arg1.Value != "Hello") throw new Exception("Error modifying arg1 value.");
        if (arg2.Value != "World") throw new Exception("Error modifying arg2 value.");
    }
    
    // *** Mixed arguments ***

    public void VoidMixedMethod(string arg1, ReadOnlySpan<char> arg2, Span<char> arg3, ReadOnlyRefStruct arg4)
    {
        if (arg1 != "Hello") throw new Exception("Error modifying arg1 value.");
        if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
        if (arg3.ToString() != "Hello") throw new Exception("Error modifying arg3 value.");
        if (arg4.Value != "World") throw new Exception("Error modifying arg4 value.");
    }

    public void VoidMixedMethod(ref string arg1, ref ReadOnlySpan<char> arg2, ref Span<char> arg3, ref ReadOnlyRefStruct arg4)
    {
        if (arg1 != "Hello") throw new Exception("Error modifying arg1 value.");
        if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
        if (arg3.ToString() != "Hello") throw new Exception("Error modifying arg3 value.");
        if (arg4.Value != "World") throw new Exception("Error modifying arg4 value.");
    }

    public void VoidMixedMethod(string arg1, ref ReadOnlySpan<char> arg2, Span<char> arg3, ref ReadOnlyRefStruct arg4)
    {
        if (arg1 != "Hello") throw new Exception("Error modifying arg1 value.");
        if (!arg2.SequenceEqual("World".AsSpan())) throw new Exception("Error modifying arg2 value.");
        if (arg3.ToString() != "Hello") throw new Exception("Error modifying arg3 value.");
        if (arg4.Value != "World") throw new Exception("Error modifying arg4 value.");
    }
}

public readonly ref struct ReadOnlyRefStruct
{
    public string Value { get; }
    
    public ReadOnlyRefStruct(string value)
    {
        Value = value;
    }
}
