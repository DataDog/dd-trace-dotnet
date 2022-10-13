using System;

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
