using System;

namespace CallTargetNativeTest
{
    internal abstract class AbstractClass
    {
        public abstract void VoidMethod(string name);
    }

    internal class Impl01OfAbstract : AbstractClass
    {
        public override void VoidMethod(string name)
        {
        }

        public void OtherMethod()
        {
            Console.WriteLine("Hello from the other method");
        }
    }

    internal class Impl02OfAbstract : AbstractClass
    {
        public override void VoidMethod(string name)
        {
        }
    }

    internal class NormalClassToDerive
    {
        public virtual void VoidMethod(string name)
        {
            Console.WriteLine("This will not be instrumented.");
        }
    }


    internal class NonAbstractClass
    {
        public virtual void VoidMethod(string name)
        {
            Console.WriteLine("Hello from NonAbstractClass");
        }
    }

    internal class Impl01OfNonAbstract : NonAbstractClass
    {
        public override void VoidMethod(string name)
        {
            Console.WriteLine("Hello from Impl01OfNonAbstract ");
        }
    }

    internal class Impl02OfNonAbstract : NonAbstractClass
    {
        public override void VoidMethod(string name)
        {
            base.VoidMethod(name);
        }
    }
}
