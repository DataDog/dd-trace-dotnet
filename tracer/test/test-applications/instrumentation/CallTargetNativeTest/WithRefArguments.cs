using System;
using System.Security;

namespace CallTargetNativeTest;

partial class Program
{
    private static void WithRefArguments()
    {
        var wRefArg = new WithRefArguments();
        Console.WriteLine($"{typeof(WithRefArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString");
        });

        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString", 15);

            if (wRefArg.StringValue != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (wRefArg.IntValue != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });

        Console.WriteLine($"{typeof(WithRefArguments).FullName}.VoidRefMethod");
        RunMethod(() =>
        {
            string strVal = "MyString";
            wRefArg.VoidRefMethod(ref strVal);

            if (strVal != "Hello world")
            {
                throw new Exception("Error modifying string value.");
            }

            var strVal2 = new ReadOnlyRefStruct("MyString");
            wRefArg.VoidRefMethod2(ref strVal2);

            if (strVal2.Value != "Hello world")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strVal = "MyString";
            int intVal = 15;

            wRefArg.VoidRefMethod(ref strVal, ref intVal);

            if (strVal != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intVal != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }

    private static void ParentWithRefArguments()
    {
        var wRefArg = new ArgumentsParentType.WithRefArguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.WithRefArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString");
        });

        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString", 15);

            if (wRefArg.StringValue != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (wRefArg.IntValue != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });

        Console.WriteLine($"{typeof(ArgumentsParentType.WithRefArguments).FullName}.VoidRefMethod");
        RunMethod(() =>
        {
            string strVal = "MyString";
            wRefArg.VoidRefMethod(ref strVal);

            if (strVal != "Hello world")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strVal = "MyString";
            int intVal = 15;

            wRefArg.VoidRefMethod(ref strVal, ref intVal);

            if (strVal != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intVal != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }

    private static void StructParentWithRefArguments()
    {
        var wRefArg = new ArgumentsStructParentType.WithRefArguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.WithRefArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString");
        });

        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString", 15);

            if (wRefArg.StringValue != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (wRefArg.IntValue != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });

        Console.WriteLine($"{typeof(ArgumentsStructParentType.WithRefArguments).FullName}.VoidRefMethod");
        RunMethod(() =>
        {
            string strVal = "MyString";
            wRefArg.VoidRefMethod(ref strVal);

            if (strVal != "Hello world")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strVal = "MyString";
            int intVal = 15;

            wRefArg.VoidRefMethod(ref strVal, ref intVal);

            if (strVal != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intVal != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }

    private static void GenericParentWithRefArguments()
    {
        var wRefArg = new ArgumentsGenericParentType<object>.WithRefArguments();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.WithRefArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString");
        });

        RunMethod(() =>
        {
            wRefArg.VoidMethod("MyString", 15);

            if (wRefArg.StringValue != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (wRefArg.IntValue != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });

        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.WithRefArguments).FullName}.VoidRefMethod");
        RunMethod(() =>
        {
            string strVal = "MyString";
            wRefArg.VoidRefMethod(ref strVal);

            if (strVal != "Hello world")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strVal = "MyString";
            int intVal = 15;

            wRefArg.VoidRefMethod(ref strVal, ref intVal);

            if (strVal != "MyString (Modified)")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intVal != 42)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }
}

internal class WithRefArguments
{
    public string StringValue { get; set; }
    public int IntValue { get; set; }

    public void VoidMethod(string arg1, int arg2)
    {
        StringValue = arg1;
        IntValue = arg2;
    }

    [SecurityCritical]
    public void VoidRefMethod(ref string arg1, ref int arg2)
    {
        StringValue = arg1;
        IntValue = arg2;
    }


    public void VoidMethod(string arg1)
    {
    }

    [SecurityCritical]
    public void VoidRefMethod(ref string arg1)
    {
        arg1 = "Hello world";
    }

    public void VoidRefMethod2(ref ReadOnlyRefStruct arg1)
    {
    }
}

public readonly ref struct ReadOnlyRefStruct
{
    public readonly string Value { get; }
    
    public ReadOnlyRefStruct(string value)
    {
        Value = value;
    }
}

partial class ArgumentsParentType
{
    public class WithRefArguments
    {
        public string StringValue { get; set; }
        public int IntValue { get; set; }

        public void VoidMethod(string arg1, int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }

        public void VoidRefMethod(ref string arg1, ref int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }


        public void VoidMethod(string arg1)
        {
        }

        [SecurityCritical]
        public void VoidRefMethod(ref string arg1)
        {
            arg1 = "Hello world";
        }
    }
}

partial struct ArgumentsStructParentType
{
    public class WithRefArguments
    {
        public string StringValue { get; set; }
        public int IntValue { get; set; }

        public void VoidMethod(string arg1, int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }

        public void VoidRefMethod(ref string arg1, ref int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }


        public void VoidMethod(string arg1)
        {
        }

        public void VoidRefMethod(ref string arg1)
        {
            arg1 = "Hello world";
        }
    }
}

partial class ArgumentsGenericParentType<PType>
{
    public class WithRefArguments
    {
        public string StringValue { get; set; }
        public int IntValue { get; set; }

        public void VoidMethod(string arg1, int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }

        [SecurityCritical]
        public void VoidRefMethod(ref string arg1, ref int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }


        public void VoidMethod(string arg1)
        {
        }

        [SecurityCritical]
        public void VoidRefMethod(ref string arg1)
        {
            arg1 = "Hello world";
        }
    }
}
