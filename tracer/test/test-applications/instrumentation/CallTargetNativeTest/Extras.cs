using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void Extras()
    {
        Extras extras = new Extras();
        Console.WriteLine($"{typeof(Extras).FullName}.{nameof(extras.NonVoidWithBranchToLastReturn)}");
        RunMethod(() => extras.NonVoidWithBranchToLastReturn());
    }
}

public class Extras
{
    private static readonly Random _random = new();
    public int NonVoidWithBranchToLastReturn()
    {
        var result = _random.Next();
        if (result % 2 == 0)
        {
            Console.WriteLine("Is Even");
        }

        return result;
    }
}
