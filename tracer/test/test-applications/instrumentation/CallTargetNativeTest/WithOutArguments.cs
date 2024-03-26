using System;

namespace CallTargetNativeTest;

partial class Program
{
    private static void WithOutArguments(bool checkInstrumented = true)
    {
        var wOutArg = new WithOutArguments();
        Console.WriteLine($"{typeof(WithOutArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            string strValue;
            wOutArg.VoidMethod(out strValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }
        }, checkInstrumented);
        RunMethod(() =>
        {
            string strValue;
            int intValue;

            wOutArg.VoidMethod(out strValue, out intValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intValue != 12)
            {
                throw new Exception("Error modifying int value.");
            }
        }, checkInstrumented);
    }

    private static void ParentWithOutArguments()
    {
        var wOutArg = new ArgumentsParentType.WithOutArguments();
        Console.WriteLine($"{typeof(ArgumentsParentType.WithOutArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            string strValue;
            wOutArg.VoidMethod(out strValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strValue;
            int intValue;

            wOutArg.VoidMethod(out strValue, out intValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intValue != 12)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }

    private static void StructParentWithOutArguments()
    {
        var wOutArg = new ArgumentsStructParentType.WithOutArguments();
        Console.WriteLine($"{typeof(ArgumentsStructParentType.WithOutArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            string strValue;
            wOutArg.VoidMethod(out strValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strValue;
            int intValue;

            wOutArg.VoidMethod(out strValue, out intValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intValue != 12)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }

    private static void GenericParentWithOutArguments()
    {
        var wOutArg = new ArgumentsGenericParentType<object>.WithOutArguments();
        Console.WriteLine($"{typeof(ArgumentsGenericParentType<object>.WithOutArguments).FullName}.VoidMethod");
        RunMethod(() =>
        {
            string strValue;
            wOutArg.VoidMethod(out strValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }
        });
        RunMethod(() =>
        {
            string strValue;
            int intValue;

            wOutArg.VoidMethod(out strValue, out intValue);

            if (strValue != "Arg01")
            {
                throw new Exception("Error modifying string value.");
            }

            if (intValue != 12)
            {
                throw new Exception("Error modifying int value.");
            }
        });
    }
}

internal class WithOutArguments
{
    public void VoidMethod(out string arg1)
    {
        arg1 = "Arg01";
    }

    public void VoidMethod(out string arg1, out int arg2)
    {
        arg1 = "Arg01";
        arg2 = 12;
    }
}

partial class ArgumentsParentType
{
    public class WithOutArguments
    {
        public void VoidMethod(out string arg1)
        {
            arg1 = "Arg01";
        }

        public void VoidMethod(out string arg1, out int arg2)
        {
            arg1 = "Arg01";
            arg2 = 12;
        }
    }
}

partial struct ArgumentsStructParentType
{
    public class WithOutArguments
    {
        public void VoidMethod(out string arg1)
        {
            arg1 = "Arg01";
        }

        public void VoidMethod(out string arg1, out int arg2)
        {
            arg1 = "Arg01";
            arg2 = 12;
        }
    }
}

partial class ArgumentsGenericParentType<PType>
{
    public class WithOutArguments
    {
        public void VoidMethod(out string arg1)
        {
            arg1 = "Arg01";
        }

        public void VoidMethod(out string arg1, out int arg2)
        {
            arg1 = "Arg01";
            arg2 = 12;
        }
    }
}
