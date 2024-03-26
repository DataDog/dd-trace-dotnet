using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest;

partial class Program
{
    private static void InterfaceMethod()
    {
        InterfaceType implicitImpl = new ImplicitImplInterface();
        Console.WriteLine($"{typeof(ImplicitImplInterface).FullName}.VoidMethod");
        RunMethod(() => implicitImpl.VoidMethod("Hello World"));

        InterfaceType explicitImpl = new ExplicitImplInterface();
        Console.WriteLine($"{typeof(ExplicitImplInterface).FullName}.VoidMethod");
        RunMethod(() => explicitImpl.VoidMethod("Hello World"));

        // Note: This demonstrates that, through interface instrumentation, the instrumented type must directly implement the interface
        // This case requires additional derived instrumentation
        InterfaceType derivedImplicit = new DerivedImplicit();
        Console.WriteLine($"{typeof(DerivedImplicit).FullName}.VoidMethod");
        RunMethod(() => derivedImplicit.VoidMethod("Hello World"), checkInstrumented: false);
        
        // Check we always select the explicit implementation over a normal implementation.
        IExplicitOverNormal explicitOverNormalImpl = new ExplicitOverNormalImpl();
        Console.WriteLine($"{typeof(ExplicitOverNormalImpl).FullName}.ReturnValueMethod");
        RunMethod(() =>
        {
            var value = explicitOverNormalImpl.ReturnValueMethod();
            if (value != "Hello World")
            {
                throw new Exception("Error! Incorrect instrumentation.");
            }
        });
    }
}

internal interface InterfaceType
{
    void VoidMethod(string name);
}

internal class ImplicitImplInterface : InterfaceType
{
    public virtual void VoidMethod(string name)
    {
    }
}

internal class ExplicitImplInterface : InterfaceType
{
    void InterfaceType.VoidMethod(string name)
    {
    }
}

internal class DerivedImplicit : ImplicitImplInterface
{
    public override void VoidMethod(string name)
    {
    }
}

internal interface IExplicitOverNormal
{
    string ReturnValueMethod();
}

internal class ExplicitOverNormalImpl : IExplicitOverNormal
{
    string IExplicitOverNormal.ReturnValueMethod()
    {
        return "Hello";
    }

    public string ReturnValueMethod()
    {
        return null;
    }
}

public static class ExplicitOverNormalIntegration
{
    public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(ExplicitOverNormalIntegration)}, {typeof(TTarget)}>({instance})");
        return CallTargetState.GetDefault();
    }

    public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        if (returnValue is "Hello")
        {
            Console.WriteLine($"ProfilerOK: EndMethod(1)<{typeof(ExplicitOverNormalIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return new CallTargetReturn<TReturn>((TReturn)(object)"Hello World");
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
