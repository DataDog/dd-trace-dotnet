using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest;

partial class Program
{
    private static void InstrumentationExceptions()
    {
        var duckTypeExceptions = new InstrumentationExceptionDuckTypeExceptionThrowOnBegin();
        Console.WriteLine($"{typeof(InstrumentationExceptionDuckTypeExceptionThrowOnBegin).FullName}.{nameof(InstrumentationExceptionDuckTypeExceptionThrowOnBegin.DoSomething)}");
        RunMethod(duckTypeExceptions.DoSomething, expectEndMethodExecution: false);

        var missingMethodExceptions = new InstrumentationExceptionMissingMethodExceptionThrowOnBegin();
        Console.WriteLine($"{typeof(InstrumentationExceptionMissingMethodExceptionThrowOnBegin).FullName}.{nameof(InstrumentationExceptionMissingMethodExceptionThrowOnBegin.DoSomething)}");
        RunMethod(missingMethodExceptions.DoSomething, expectEndMethodExecution: false);

        var callTargetInvokerExceptions = new InstrumentationExceptionCallTargetInvokerExceptionThrowOnBegin();
        Console.WriteLine($"{typeof(InstrumentationExceptionCallTargetInvokerExceptionThrowOnBegin).FullName}.{nameof(InstrumentationExceptionCallTargetInvokerExceptionThrowOnBegin.DoSomething)}");
        RunMethod(callTargetInvokerExceptions.DoSomething, expectEndMethodExecution: false);

        var duckTypeExceptionsOnEnd = new InstrumentationExceptionDuckTypeExceptionThrowOnEnd();
        Console.WriteLine($"{typeof(InstrumentationExceptionDuckTypeExceptionThrowOnEnd).FullName}.{nameof(InstrumentationExceptionDuckTypeExceptionThrowOnEnd.DoSomething)}");
        RunMethod(duckTypeExceptionsOnEnd.DoSomething);

        var duckTypeExceptionsOnAsyncEnd = new InstrumentationExceptionDuckTypeExceptionThrowOnAsyncEnd();
        Console.WriteLine($"{typeof(InstrumentationExceptionDuckTypeExceptionThrowOnAsyncEnd).FullName}.{nameof(InstrumentationExceptionDuckTypeExceptionThrowOnAsyncEnd.DoSomething)}");
        RunMethod(() => duckTypeExceptionsOnAsyncEnd.DoSomething().Wait(), asyncMethod: true);
    }
}

public class InstrumentationExceptionDuckTypeExceptionThrowOnBegin
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw an instrumentation exception");
    }
}

public class InstrumentationExceptionMissingMethodExceptionThrowOnBegin
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw an instrumentation exception");
    }
}

public class InstrumentationExceptionCallTargetInvokerExceptionThrowOnBegin
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw an instrumentation exception");
    }
}

public class InstrumentationExceptionDuckTypeExceptionThrowOnEnd
{
    public void DoSomething()
    {
        Console.WriteLine("Instrumentation will throw an instrumentation exception");
    }
}

public class InstrumentationExceptionDuckTypeExceptionThrowOnAsyncEnd
{
    public Task DoSomething()
    {
        Console.WriteLine("Instrumentation will throw an instrumentation exception");
        return Task.CompletedTask;
    }
}
