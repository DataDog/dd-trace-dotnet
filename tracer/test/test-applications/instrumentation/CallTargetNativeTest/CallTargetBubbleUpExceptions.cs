using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest;

partial class Program
{
    private static void CallTargetBubbleUpExceptions()
    {
        var callTargetBubbleUpExceptions = new CallTargetBubbleUpExceptionsThrowBubbleUpOnBegin();
        Console.WriteLine($"{typeof(CallTargetBubbleUpExceptionsThrowBubbleUpOnBegin).FullName}.{nameof(CallTargetBubbleUpExceptionsThrowBubbleUpOnBegin.DoSomething)}");
        RunMethod(callTargetBubbleUpExceptions.DoSomething, bubblingUpException: true);
        
        var callTargetBubbleUpExceptionsNestedOnBegin = new CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnBegin();
        Console.WriteLine($"{typeof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnBegin).FullName}.{nameof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnBegin.DoSomething)}");
        RunMethod(callTargetBubbleUpExceptionsNestedOnBegin.DoSomething, bubblingUpException: true);
        
        var callTargetBubbleUpExceptionsThrowBubbleUpOnEnd = new CallTargetBubbleUpExceptionsThrowBubbleUpOnEnd();
        Console.WriteLine($"{typeof(CallTargetBubbleUpExceptionsThrowBubbleUpOnEnd).FullName}.{nameof(CallTargetBubbleUpExceptionsThrowBubbleUpOnEnd.DoSomething)}");
        RunMethod(callTargetBubbleUpExceptionsThrowBubbleUpOnEnd.DoSomething, bubblingUpException: true);
        
        var callTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd = new CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd();
        Console.WriteLine($"{typeof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd).FullName}.{nameof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd.DoSomething)}");
        RunMethod(callTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd.DoSomething, bubblingUpException: true);

        var callTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd = new CallTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd();
        Console.WriteLine($"{typeof(CallTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd).FullName}.{nameof(CallTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd.DoSomething)}");
        RunMethod(() => callTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd.DoSomething().Wait(), bubblingUpException: true);
        
        var callTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd = new CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd();
        Console.WriteLine($"{typeof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd).FullName}.{nameof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd.DoSomething)}");
        RunMethod(() => callTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd.DoSomething().Wait(), bubblingUpException: true);
    }
}

public class CallTargetBubbleUpExceptionsThrowBubbleUpOnBegin
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw a bubble up");
    }
}

public class CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnBegin
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw a bubble up");
    }
}

public class CallTargetBubbleUpExceptionsThrowBubbleUpOnEnd
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw a bubble up");
    }
}

public class CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw a bubble up");
    }
}

public class CallTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd
{
    public Task DoSomething()
    {
        Console.WriteLine("Instrumentation will throw a bubble up");
        return Task.CompletedTask;
    }
}

public class CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd
{
    public Task DoSomething()
    {
        Console.WriteLine("Instrumentation will throw a bubble up");
        return Task.CompletedTask;
    }
}
