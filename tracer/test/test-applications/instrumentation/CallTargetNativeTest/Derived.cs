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

    partial class ArgumentsParentType
    {
        public abstract class AbstractClass
        {
            public abstract void VoidMethod(string name);
        }

        public class Impl01OfAbstract : ArgumentsParentType.AbstractClass
        {
            public override void VoidMethod(string name)
            {
            }

            public void OtherMethod()
            {
                Console.WriteLine("Hello from the other method");
            }
        }

        public class Impl02OfAbstract : ArgumentsParentType.AbstractClass
        {
            public override void VoidMethod(string name)
            {
            }
        }

        public class NonAbstractClass
        {
            public virtual void VoidMethod(string name)
            {
                Console.WriteLine("Hello from NonAbstractClass");
            }
        }

        public class Impl01OfNonAbstract : ArgumentsParentType.NonAbstractClass
        {
            public override void VoidMethod(string name)
            {
                Console.WriteLine("Hello from Impl01OfNonAbstract ");
            }
        }

        public class Impl02OfNonAbstract : ArgumentsParentType.NonAbstractClass
        {
            public override void VoidMethod(string name)
            {
                base.VoidMethod(name);
            }
        }
    }
}
