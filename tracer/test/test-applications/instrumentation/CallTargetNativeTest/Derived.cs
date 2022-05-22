using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void AbstractMethod()
    {
        var impl01 = new Impl01OfAbstract();
        Console.WriteLine($"{typeof(Impl01OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl01.VoidMethod("Hello World"));

        Console.WriteLine($"{typeof(Impl01OfAbstract).FullName}.OtherMethod");
        RunMethod(() => impl01.OtherMethod());

        var impl02 = new Impl02OfAbstract();
        Console.WriteLine($"{typeof(Impl02OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl02.VoidMethod("Hello World"));

        var implN01 = new Impl01OfNonAbstract();
        Console.WriteLine($"{typeof(Impl01OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN01.VoidMethod("Hello World"));

        var implN02 = new Impl02OfNonAbstract();
        Console.WriteLine($"{typeof(Impl02OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN02.VoidMethod("Hello World"));
    }

    private static void ParentAbstractMethod()
    {
        var impl01 = new ArgumentsParentType.Impl01OfAbstract();
        Console.WriteLine($"{typeof(ArgumentsParentType.Impl01OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl01.VoidMethod("Hello World"));

        Console.WriteLine($"{typeof(ArgumentsParentType.Impl01OfAbstract).FullName}.OtherMethod");
        RunMethod(() => impl01.OtherMethod());

        var impl02 = new ArgumentsParentType.Impl02OfAbstract();
        Console.WriteLine($"{typeof(ArgumentsParentType.Impl02OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl02.VoidMethod("Hello World"));

        var implN01 = new ArgumentsParentType.Impl01OfNonAbstract();
        Console.WriteLine($"{typeof(ArgumentsParentType.Impl01OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN01.VoidMethod("Hello World"));

        var implN02 = new ArgumentsParentType.Impl02OfNonAbstract();
        Console.WriteLine($"{typeof(ArgumentsParentType.Impl02OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN02.VoidMethod("Hello World"));
    }

    private static void StructParentAbstractMethod()
    {
        var impl01 = new ArgumentsStructParentType.Impl01OfAbstract();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.Impl01OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl01.VoidMethod("Hello World"));

        Console.WriteLine($"{typeof(ArgumentsStructParentType.Impl01OfAbstract).FullName}.OtherMethod");
        RunMethod(() => impl01.OtherMethod());

        var impl02 = new ArgumentsStructParentType.Impl02OfAbstract();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.Impl02OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl02.VoidMethod("Hello World"));

        var implN01 = new ArgumentsStructParentType.Impl01OfNonAbstract();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.Impl01OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN01.VoidMethod("Hello World"));

        var implN02 = new ArgumentsStructParentType.Impl02OfNonAbstract();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.Impl02OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN02.VoidMethod("Hello World"));
    }

    private static void GenericParentAbstractMethod()
    {
        var impl01 = new ArgumentsGenericParentType<object>.Impl01OfAbstract();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.Impl01OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl01.VoidMethod("Hello World"));

        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.Impl01OfAbstract).FullName}.OtherMethod");
        RunMethod(() => impl01.OtherMethod());

        var impl02 = new ArgumentsGenericParentType<object>.Impl02OfAbstract();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.Impl02OfAbstract).FullName}.VoidMethod");
        RunMethod(() => impl02.VoidMethod("Hello World"));

        var implN01 = new ArgumentsGenericParentType<object>.Impl01OfNonAbstract();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.Impl01OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN01.VoidMethod("Hello World"));

        var implN02 = new ArgumentsGenericParentType<object>.Impl02OfNonAbstract();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.Impl02OfNonAbstract).FullName}.VoidMethod");
        RunMethod(() => implN02.VoidMethod("Hello World"));
    }
}

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

partial struct ArgumentsStructParentType
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

partial class ArgumentsGenericParentType<PType>
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
