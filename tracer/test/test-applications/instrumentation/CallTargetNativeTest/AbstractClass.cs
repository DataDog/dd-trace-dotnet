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
}
